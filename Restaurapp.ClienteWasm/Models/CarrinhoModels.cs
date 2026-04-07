namespace Restaurapp.ClienteWasm.Models
{
    public class CarrinhoItemState
    {
        public int ProdutoId { get; set; }
        public string NomeProduto { get; set; } = string.Empty;
        public decimal PrecoUnitario { get; set; }
        public int Quantidade { get; set; }
    }

    public class CarrinhoState
    {
        public int EmpresaId { get; set; }
        public string EmpresaNome { get; set; } = string.Empty;
        public List<CarrinhoItemState> Itens { get; set; } = new();
    }

    public class CarrinhoSnapshot
    {
        public int EmpresaId { get; set; }
        public string EmpresaNome { get; set; } = string.Empty;
        public List<CarrinhoItemState> Itens { get; set; } = new();
        public int QuantidadeTotalItens { get; set; }
        public decimal Subtotal { get; set; }
    }
}