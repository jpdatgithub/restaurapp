using Microsoft.AspNetCore.SignalR;
using Restaurapp.BlazorServer.Hubs;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public class ServicoTempoRealPedidos
    {
        private readonly IHubContext<HubPedidosTempoReal> _hubContext;
        private readonly CanalAtualizacaoPedidos _canalAtualizacaoPedidos;

        public ServicoTempoRealPedidos(
            IHubContext<HubPedidosTempoReal> hubContext,
            CanalAtualizacaoPedidos canalAtualizacaoPedidos)
        {
            _hubContext = hubContext;
            _canalAtualizacaoPedidos = canalAtualizacaoPedidos;
        }

        public async Task NotificarPedidoAtualizadoAsync(Pedido pedido)
        {
            var evento = new
            {
                pedido.Id,
                pedido.EmpresaId,
                pedido.ClienteUsuarioId,
                Status = pedido.Status.ToString(),
                DataAtualizacaoUtc = pedido.UpdatedAtUtc
            };

            await _hubContext.Clients
                .Group(HubPedidosTempoReal.ObterGrupoEmpresa(pedido.EmpresaId))
                .SendAsync("pedido_atualizado", evento);

            await _hubContext.Clients
                .Group(HubPedidosTempoReal.ObterGrupoCliente(pedido.ClienteUsuarioId))
                .SendAsync("pedido_atualizado", evento);

            await _canalAtualizacaoPedidos.PublicarAsync(new AtualizacaoPedidoNotificacao
            {
                PedidoId = pedido.Id,
                EmpresaId = pedido.EmpresaId,
                ClienteUsuarioId = pedido.ClienteUsuarioId,
                Status = pedido.Status
            });
        }

        public async Task NotificarContaAbertaAtualizadaAsync(int clienteUsuarioId)
        {
            await _hubContext.Clients
                .Group(HubPedidosTempoReal.ObterGrupoCliente(clienteUsuarioId))
                .SendAsync("conta_aberta_atualizada");
        }
    }
}