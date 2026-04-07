using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public class PedidoService
    {
        public sealed record VendaAvulsaItemInput(int ProdutoId, int Quantidade);
        public sealed record ResultadoPaginado<T>(List<T> Itens, int TotalRegistros);

        private readonly AppDbContext _context;

        public PedidoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Pedido>> ObterPedidosAsync(
            StatusPedido? status = null,
            DateTime? dataInicialUtc = null,
            DateTime? dataFinalUtc = null,
            bool ordenarPorAtualizacao = false)
        {
            IQueryable<Pedido> query = _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Itens)
                .Include(p => p.HistoricoStatus);

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            if (dataInicialUtc.HasValue)
            {
                query = query.Where(p => p.CreatedAtUtc >= dataInicialUtc.Value);
            }

            if (dataFinalUtc.HasValue)
            {
                query = query.Where(p => p.CreatedAtUtc <= dataFinalUtc.Value);
            }

            query = AplicarOrdenacao(query, ordenarPorAtualizacao);
            return await query.ToListAsync();
        }

        public async Task<ResultadoPaginado<Pedido>> ObterPedidosPaginadosAsync(
            int pagina,
            int tamanhoPagina,
            StatusPedido? status = null,
            DateTime? dataInicialUtc = null,
            DateTime? dataFinalUtc = null,
            bool ordenarPorAtualizacao = false)
        {
            if (pagina < 1)
            {
                pagina = 1;
            }

            if (tamanhoPagina < 1)
            {
                tamanhoPagina = 5;
            }

            IQueryable<Pedido> query = _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Itens)
                .Include(p => p.HistoricoStatus);

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            if (dataInicialUtc.HasValue)
            {
                query = query.Where(p => p.CreatedAtUtc >= dataInicialUtc.Value);
            }

            if (dataFinalUtc.HasValue)
            {
                query = query.Where(p => p.CreatedAtUtc <= dataFinalUtc.Value);
            }

            var totalRegistros = await query.CountAsync();
            var itens = await AplicarOrdenacao(query, ordenarPorAtualizacao)
                .Skip((pagina - 1) * tamanhoPagina)
                .Take(tamanhoPagina)
                .ToListAsync();

            return new ResultadoPaginado<Pedido>(itens, totalRegistros);
        }

        private static IQueryable<Pedido> AplicarOrdenacao(IQueryable<Pedido> query, bool ordenarPorAtualizacao)
        {
            return ordenarPorAtualizacao
                ? query.OrderByDescending(p => p.UpdatedAtUtc)
                    .ThenByDescending(p => p.CreatedAtUtc)
                : query.OrderBy(p => p.CreatedAtUtc)
                    .ThenBy(p => p.Id);
        }

        public async Task<Pedido?> ObterPedidoPorIdAsync(int id)
        {
            return await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Itens)
                .Include(p => p.HistoricoStatus)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<ContaMesa>> ObterMesasEmAbertoAsync()
        {
            return await CriarQueryContasVisiveis()
                .Where(c => c.Status == StatusContaMesa.Aberta)
                .OrderBy(c => c.NumeroMesa)
                .ThenByDescending(c => c.UpdatedAtUtc)
                .ThenByDescending(c => c.CreatedAtUtc)
                .ToListAsync();
        }

        public async Task<List<ContaMesa>> ObterTodasContasMesaAsync()
        {
            return await CriarQueryContasVisiveis()
                .OrderByDescending(c => c.UpdatedAtUtc)
                .ThenByDescending(c => c.CreatedAtUtc)
                .ToListAsync();
        }

        public async Task<ResultadoPaginado<ContaMesa>> ObterTodasContasMesaPaginadasAsync(
            int pagina,
            int tamanhoPagina,
            string? filtro = null)
        {
            if (pagina < 1)
            {
                pagina = 1;
            }

            if (tamanhoPagina < 1)
            {
                tamanhoPagina = 5;
            }

            IQueryable<ContaMesa> query = CriarQueryContasVisiveis();

            if (!string.IsNullOrWhiteSpace(filtro))
            {
                var termo = filtro.Trim();
                query = query.Where(c =>
                    c.NumeroMesa.Contains(termo)
                    || c.Id.ToString().Contains(termo)
                    || c.Pedidos.Any(p => p.NomeClienteSnapshot.Contains(termo)));
            }

            var totalRegistros = await query.CountAsync();
            var itens = await query
                .OrderByDescending(c => c.UpdatedAtUtc)
                .ThenByDescending(c => c.CreatedAtUtc)
                .Skip((pagina - 1) * tamanhoPagina)
                .Take(tamanhoPagina)
                .ToListAsync();

            return new ResultadoPaginado<ContaMesa>(itens, totalRegistros);
        }

        public async Task<ContaMesa?> ObterContaMesaDetalhadaAsync(int contaMesaId)
        {
            return await CriarQueryContasVisiveis(incluirItens: true)
                .FirstOrDefaultAsync(c => c.Id == contaMesaId);
        }

        private IQueryable<ContaMesa> CriarQueryContasVisiveis(bool incluirItens = false)
        {
            IQueryable<ContaMesa> query = _context.ContasMesa
                .AsNoTracking()
                .Where(c => c.Pedidos.Any(p => p.Status != StatusPedido.Cancelado && p.Status != StatusPedido.Criado));

            query = incluirItens
                ? query.Include(c => c.Pedidos.Where(p => p.Status != StatusPedido.Cancelado))
                    .ThenInclude(p => p.Itens)
                : query.Include(c => c.Pedidos.Where(p => p.Status != StatusPedido.Cancelado));

            return query;
        }

        public async Task<bool> FecharContaMesaAsync(int contaMesaId)
        {
            var conta = await _context.ContasMesa
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == contaMesaId && c.Status == StatusContaMesa.Aberta);

            if (conta is null)
            {
                return false;
            }

            var agoraUtc = DateTime.UtcNow;
            conta.Status = StatusContaMesa.Fechada;
            conta.UpdatedAtUtc = agoraUtc;
            conta.FechadaAtUtc = agoraUtc;

            var totalConta = await _context.Pedidos
                .IgnoreQueryFilters()
                .Where(p => p.ContaMesaId == contaMesaId && p.Status != StatusPedido.Cancelado)
                .SumAsync(p => p.Total);

            if (totalConta > 0)
            {
                _context.Transacoes.Add(new Transacao
                {
                    EmpresaId = conta.EmpresaId,
                    Categoria = CategoriaDeTransacao.Receita,
                    Descricao = $"Pagamento da conta #{conta.Id} (Mesa {conta.NumeroMesa})",
                    Valor = totalConta,
                    DataDeCadastro = agoraUtc,
                    DataRetroativa = null
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Pedido?> RegistrarVendaAvulsaAsync(List<VendaAvulsaItemInput> itens, int? contaMesaId = null)
        {
            if (itens.Count == 0 || _context.EmpresaId <= 0)
            {
                return null;
            }

            ContaMesa? contaMesaSelecionada = null;
            if (contaMesaId.HasValue)
            {
                contaMesaSelecionada = await _context.ContasMesa
                    .FirstOrDefaultAsync(c =>
                        c.Id == contaMesaId.Value
                        && c.EmpresaId == _context.EmpresaId
                        && c.Status == StatusContaMesa.Aberta);

                if (contaMesaSelecionada is null)
                {
                    return null;
                }
            }

            var itensNormalizados = itens
                .Where(i => i.ProdutoId > 0 && i.Quantidade > 0)
                .GroupBy(i => i.ProdutoId)
                .Select(g => new VendaAvulsaItemInput(g.Key, g.Sum(x => x.Quantidade)))
                .ToList();

            if (itensNormalizados.Count == 0)
            {
                return null;
            }

            var produtosIds = itensNormalizados.Select(i => i.ProdutoId).ToList();
            var produtos = await _context.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo && produtosIds.Contains(p.Id))
                .ToListAsync();

            if (produtos.Count != produtosIds.Count)
            {
                return null;
            }

            var itensPedido = itensNormalizados
                .Join(produtos,
                    i => i.ProdutoId,
                    p => p.Id,
                    (item, produto) => new ItemDePedido
                    {
                        ProdutoId = produto.Id,
                        NomeProdutoSnapshot = produto.Nome,
                        PrecoUnitarioSnapshot = produto.Preco,
                        Quantidade = item.Quantidade,
                        SubtotalItem = produto.Preco * item.Quantidade
                    })
                .ToList();

            var agoraUtc = DateTime.UtcNow;
            var subtotal = itensPedido.Sum(i => i.SubtotalItem);

            var pedido = new Pedido
            {
                EmpresaId = _context.EmpresaId,
                ClienteUsuarioId = 0,
                ContaMesaId = contaMesaSelecionada?.Id,
                NomeClienteSnapshot = "Venda avulsa",
                EmailClienteSnapshot = "venda-avulsa@local",
                Status = StatusPedido.VendaAvulsa,
                TipoAtendimento = contaMesaSelecionada is null ? TipoAtendimentoPedido.Retirada : TipoAtendimentoPedido.ComerAqui,
                Origem = OrigemPedido.AplicativoClienteAutenticado,
                NumeroMesa = contaMesaSelecionada?.NumeroMesa,
                Takeaway = contaMesaSelecionada is null,
                Moeda = "BRL",
                Subtotal = subtotal,
                Desconto = 0m,
                Frete = 0m,
                Total = subtotal,
                CreatedAtUtc = agoraUtc,
                UpdatedAtUtc = agoraUtc,
                Itens = itensPedido,
                HistoricoStatus = new List<HistoricoStatusPedido>
                {
                    new()
                    {
                        Status = StatusPedido.VendaAvulsa,
                        DataStatusUtc = agoraUtc,
                        RegistradoPor = "Empresa"
                    }
                }
            };

            if (contaMesaSelecionada is not null)
            {
                contaMesaSelecionada.UpdatedAtUtc = agoraUtc;
            }

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            if (contaMesaSelecionada is null)
            {
                _context.Transacoes.Add(new Transacao
                {
                    EmpresaId = _context.EmpresaId,
                    Categoria = CategoriaDeTransacao.Receita,
                    Descricao = $"Venda avulsa - Pedido #{pedido.Id}",
                    Valor = pedido.Total,
                    DataDeCadastro = agoraUtc,
                    DataRetroativa = null
                });

                await _context.SaveChangesAsync();
            }

            return pedido;
        }

        public async Task<Pedido?> RegistrarPedidoRapidoAsync(
            List<VendaAvulsaItemInput> itens,
            TipoAtendimentoPedido tipoAtendimento,
            string? nomeCliente,
            bool takeaway = false,
            int? contaMesaId = null,
            bool criarNovaConta = false,
            string? numeroMesaNovaConta = null,
            string? enderecoEntrega = null)
        {
            if (itens.Count == 0 || _context.EmpresaId <= 0)
            {
                return null;
            }

            if (tipoAtendimento != TipoAtendimentoPedido.ComerAqui)
            {
                contaMesaId = null;
                criarNovaConta = false;
                numeroMesaNovaConta = null;
            }

            ContaMesa? contaMesaSelecionada = null;
            if (tipoAtendimento == TipoAtendimentoPedido.ComerAqui)
            {
                if (criarNovaConta)
                {
                    if (string.IsNullOrWhiteSpace(numeroMesaNovaConta))
                    {
                        return null;
                    }

                    var numeroMesaNormalizado = numeroMesaNovaConta.Trim();
                    var agoraContaUtc = DateTime.UtcNow;

                    contaMesaSelecionada = new ContaMesa
                    {
                        EmpresaId = _context.EmpresaId,
                        ClienteUsuarioId = 0,
                        NumeroMesa = numeroMesaNormalizado,
                        Status = StatusContaMesa.Aberta,
                        CreatedAtUtc = agoraContaUtc,
                        UpdatedAtUtc = agoraContaUtc,
                        FechadaAtUtc = null
                    };

                    _context.ContasMesa.Add(contaMesaSelecionada);
                }
                else
                {
                    if (!contaMesaId.HasValue)
                    {
                        return null;
                    }

                    contaMesaSelecionada = await _context.ContasMesa
                        .FirstOrDefaultAsync(c =>
                            c.Id == contaMesaId.Value
                            && c.EmpresaId == _context.EmpresaId
                            && c.Status == StatusContaMesa.Aberta);

                    if (contaMesaSelecionada is null)
                    {
                        return null;
                    }
                }
            }

            var itensNormalizados = itens
                .Where(i => i.ProdutoId > 0 && i.Quantidade > 0)
                .GroupBy(i => i.ProdutoId)
                .Select(g => new VendaAvulsaItemInput(g.Key, g.Sum(x => x.Quantidade)))
                .ToList();

            if (itensNormalizados.Count == 0)
            {
                return null;
            }

            var produtosIds = itensNormalizados.Select(i => i.ProdutoId).ToList();
            var produtos = await _context.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo && produtosIds.Contains(p.Id))
                .ToListAsync();

            if (produtos.Count != produtosIds.Count)
            {
                return null;
            }

            var itensPedido = itensNormalizados
                .Join(produtos,
                    i => i.ProdutoId,
                    p => p.Id,
                    (item, produto) => new ItemDePedido
                    {
                        ProdutoId = produto.Id,
                        NomeProdutoSnapshot = produto.Nome,
                        PrecoUnitarioSnapshot = produto.Preco,
                        Quantidade = item.Quantidade,
                        SubtotalItem = produto.Preco * item.Quantidade
                    })
                .ToList();

            var agoraUtc = DateTime.UtcNow;
            var subtotal = itensPedido.Sum(i => i.SubtotalItem);

            var nomeNormalizado = string.IsNullOrWhiteSpace(nomeCliente)
                ? (tipoAtendimento == TipoAtendimentoPedido.ComerAqui ? "Pedido em conta" : "Pedido do caixa")
                : nomeCliente.Trim();

            var pedido = new Pedido
            {
                EmpresaId = _context.EmpresaId,
                ClienteUsuarioId = 0,
                ContaMesaId = contaMesaSelecionada?.Id,
                ContaMesa = contaMesaSelecionada,
                NomeClienteSnapshot = nomeNormalizado,
                EmailClienteSnapshot = "pedido-rapido@local",
                Status = StatusPedido.Confirmado,
                TipoAtendimento = tipoAtendimento,
                Origem = OrigemPedido.AplicativoClienteAutenticado,
                NumeroMesa = tipoAtendimento == TipoAtendimentoPedido.ComerAqui ? contaMesaSelecionada?.NumeroMesa : null,
                Takeaway = takeaway,
                Moeda = "BRL",
                Subtotal = subtotal,
                Desconto = 0m,
                Frete = 0m,
                Total = subtotal,
                EnderecoEntrega = tipoAtendimento == TipoAtendimentoPedido.Entrega
                    ? (string.IsNullOrWhiteSpace(enderecoEntrega) ? null : enderecoEntrega.Trim())
                    : null,
                CreatedAtUtc = agoraUtc,
                UpdatedAtUtc = agoraUtc,
                Itens = itensPedido,
                HistoricoStatus = new List<HistoricoStatusPedido>
                {
                    new()
                    {
                        Status = StatusPedido.Confirmado,
                        DataStatusUtc = agoraUtc,
                        RegistradoPor = "Empresa"
                    }
                }
            };

            if (contaMesaSelecionada is not null)
            {
                contaMesaSelecionada.UpdatedAtUtc = agoraUtc;
            }

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            return pedido;
        }
    }
}