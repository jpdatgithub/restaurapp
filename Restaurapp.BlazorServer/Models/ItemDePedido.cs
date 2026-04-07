namespace Restaurapp.BlazorServer.Models
{
    public class ItemDePedido
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public int ProdutoId { get; set; }
        public string NomeProdutoSnapshot { get; set; } = string.Empty;
        public decimal PrecoUnitarioSnapshot { get; set; }
        public int Quantidade { get; set; }
        public decimal SubtotalItem { get; set; }
        public Pedido? Pedido { get; set; }
    }
}