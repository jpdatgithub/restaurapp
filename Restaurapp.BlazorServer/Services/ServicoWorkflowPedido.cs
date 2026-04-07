using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public class ServicoWorkflowPedido
    {
        private readonly AppDbContext _context;
        private readonly ServicoTempoRealPedidos _servicoTempoRealPedidos;

        public ServicoWorkflowPedido(
            AppDbContext context,
            ServicoTempoRealPedidos servicoTempoRealPedidos)
        {
            _context = context;
            _servicoTempoRealPedidos = servicoTempoRealPedidos;
        }

        public void RegistrarStatusInicial(Pedido pedido, string registradoPor)
        {
            if (pedido.HistoricoStatus.Count > 0)
            {
                return;
            }

            pedido.HistoricoStatus.Add(new HistoricoStatusPedido
            {
                Status = pedido.Status,
                DataStatusUtc = pedido.CreatedAtUtc,
                RegistradoPor = registradoPor
            });
        }

        public async Task<Pedido?> ObterPedidoComHistoricoAsync(int pedidoId, bool ignorarFiltros = false)
        {
            IQueryable<Pedido> query = _context.Pedidos;
            if (ignorarFiltros)
            {
                query = query.IgnoreQueryFilters();
            }

            return await query
                .Include(p => p.Itens)
                .Include(p => p.HistoricoStatus)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);
        }

        public async Task<bool> AtualizarStatusAsync(Pedido pedido, StatusPedido novoStatus, string registradoPor)
        {
            if (!TransicaoEhValida(pedido, novoStatus))
            {
                return false;
            }

            var agora = DateTime.UtcNow;
            pedido.Status = novoStatus;
            pedido.UpdatedAtUtc = agora;

            if (novoStatus == StatusPedido.Confirmado)
            {
                await GarantirContaMesaParaPedidoComerAquiAsync(pedido, agora);
            }

            pedido.HistoricoStatus.Add(new HistoricoStatusPedido
            {
                PedidoId = pedido.Id,
                Status = novoStatus,
                DataStatusUtc = agora,
                RegistradoPor = registradoPor
            });

            var precisaNotificarConta = false;
            if (pedido.TipoAtendimento == TipoAtendimentoPedido.ComerAqui && pedido.ClienteUsuarioId > 0)
            {
                precisaNotificarConta = novoStatus is StatusPedido.Confirmado or StatusPedido.Cancelado
                    || pedido.ContaMesaId.HasValue;
            }

            if (novoStatus == StatusPedido.Cancelado)
            {
                await AnularContaSeNaoHouverPedidosAtivosAsync(pedido);
            }

            await _context.SaveChangesAsync();
            await _servicoTempoRealPedidos.NotificarPedidoAtualizadoAsync(pedido);
            if (precisaNotificarConta)
            {
                await _servicoTempoRealPedidos.NotificarContaAbertaAtualizadaAsync(pedido.ClienteUsuarioId);
            }
            return true;
        }

        private async Task GarantirContaMesaParaPedidoComerAquiAsync(Pedido pedido, DateTime agoraUtc)
        {
            if (pedido.TipoAtendimento != TipoAtendimentoPedido.ComerAqui
                || string.IsNullOrWhiteSpace(pedido.NumeroMesa)
                || pedido.ClienteUsuarioId <= 0)
            {
                return;
            }

            var contasPosPagasHabilitadas = await _context.Empresas
                .Where(e => e.Id == pedido.EmpresaId)
                .Select(e => e.HabilitarContasPosPagas)
                .FirstOrDefaultAsync();

            if (!contasPosPagasHabilitadas)
            {
                return;
            }

            ContaMesa? contaMesa = null;
            if (pedido.ContaMesaId.HasValue)
            {
                contaMesa = await _context.ContasMesa
                    .FirstOrDefaultAsync(c => c.Id == pedido.ContaMesaId.Value && c.Status == StatusContaMesa.Aberta);
            }

            contaMesa ??= await _context.ContasMesa
                .Where(c => c.ClienteUsuarioId == pedido.ClienteUsuarioId && c.Status == StatusContaMesa.Aberta)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (contaMesa is null)
            {
                contaMesa = new ContaMesa
                {
                    EmpresaId = pedido.EmpresaId,
                    ClienteUsuarioId = pedido.ClienteUsuarioId,
                    NumeroMesa = pedido.NumeroMesa!.Trim(),
                    Status = StatusContaMesa.Aberta,
                    CreatedAtUtc = agoraUtc,
                    UpdatedAtUtc = agoraUtc,
                    FechadaAtUtc = null
                };

                _context.ContasMesa.Add(contaMesa);
            }
            else
            {
                contaMesa.UpdatedAtUtc = agoraUtc;
            }

            pedido.ContaMesa = contaMesa;
            if (contaMesa.Id > 0)
            {
                pedido.ContaMesaId = contaMesa.Id;
            }
        }

        private async Task AnularContaSeNaoHouverPedidosAtivosAsync(Pedido pedido)
        {
            if (!pedido.ContaMesaId.HasValue)
            {
                return;
            }

            var contaMesaId = pedido.ContaMesaId.Value;
            var conta = await _context.ContasMesa.FirstOrDefaultAsync(c => c.Id == contaMesaId);
            if (conta is null)
            {
                return;
            }

            var quantidadePedidosAtivos = await _context.Pedidos
                .CountAsync(p => p.ContaMesaId == contaMesaId
                    && p.Id != pedido.Id
                    && p.Status != StatusPedido.Cancelado);

            if (quantidadePedidosAtivos > 0)
            {
                conta.UpdatedAtUtc = DateTime.UtcNow;
                return;
            }

            _context.ContasMesa.Remove(conta);
        }

        public static bool TransicaoEhValida(Pedido pedido, StatusPedido proximo)
        {
            if (pedido.TipoAtendimento == TipoAtendimentoPedido.ComerAqui)
            {
                if (pedido.Status == StatusPedido.Confirmado && proximo == StatusPedido.Enviado)
                {
                    return true;
                }
            }

            return TransicaoEhValida(pedido.Status, proximo);
        }

        public static bool TransicaoEhValida(StatusPedido atual, StatusPedido proximo)
        {
            if (atual == proximo)
            {
                return true;
            }

            return atual switch
            {
                StatusPedido.Criado => proximo is StatusPedido.Confirmado or StatusPedido.Cancelado,
                StatusPedido.Confirmado => proximo == StatusPedido.EmPreparacao,
                StatusPedido.EmPreparacao => proximo == StatusPedido.Enviado,
                StatusPedido.Enviado => proximo == StatusPedido.Concluido,
                _ => false
            };
        }

        public static StatusPedido? ObterProximoStatusEmpresa(StatusPedido atual)
        {
            return atual switch
            {
                StatusPedido.Confirmado => StatusPedido.EmPreparacao,
                StatusPedido.EmPreparacao => StatusPedido.Enviado,
                _ => null
            };
        }

        public static StatusPedido? ObterProximoStatusEmpresa(Pedido pedido)
        {
            if (pedido.TipoAtendimento == TipoAtendimentoPedido.ComerAqui)
            {
                return pedido.Status switch
                {
                    StatusPedido.Confirmado => StatusPedido.Enviado,
                    _ => null
                };
            }

            return ObterProximoStatusEmpresa(pedido.Status);
        }
    }
}