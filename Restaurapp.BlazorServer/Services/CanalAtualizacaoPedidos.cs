using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public class AtualizacaoPedidoNotificacao
    {
        public int PedidoId { get; set; }
        public int EmpresaId { get; set; }
        public int ClienteUsuarioId { get; set; }
        public StatusPedido Status { get; set; }
    }

    public class CanalAtualizacaoPedidos
    {
        public event Func<AtualizacaoPedidoNotificacao, Task>? PedidoAtualizado;

        public Task PublicarAsync(AtualizacaoPedidoNotificacao notificacao)
        {
            if (PedidoAtualizado is null)
            {
                return Task.CompletedTask;
            }

            return PedidoAtualizado.Invoke(notificacao);
        }
    }
}