namespace Restaurapp.BlazorServer.Models
{
    public class ItemDePedidoOpcaoSnapshot
    {
        public int Id { get; set; }
        public int ItemDePedidoId { get; set; }
        public int? ProdutoOpcaoId { get; set; }
        public string NomeSecaoSnapshot { get; set; } = string.Empty;
        public string NomeOpcaoSnapshot { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal PrecoUnitarioDeltaSnapshot { get; set; }
        public decimal SubtotalDeltaSnapshot { get; set; }
        public ItemDePedido? ItemDePedido { get; set; }
    }
}