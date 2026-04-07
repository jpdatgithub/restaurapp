namespace Restaurapp.BlazorServer.Models
{
    public class HistoricoStatusPedido
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public StatusPedido Status { get; set; }
        public DateTime DataStatusUtc { get; set; }
        public string RegistradoPor { get; set; } = string.Empty;
        public Pedido? Pedido { get; set; }
    }
}