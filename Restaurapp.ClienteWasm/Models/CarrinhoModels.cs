namespace Restaurapp.ClienteWasm.Models
{
    public class CarrinhoItemOpcaoState
    {
        public int ProdutoOpcaoId { get; set; }
        public string NomeSecao { get; set; } = string.Empty;
        public string NomeOpcao { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public int QuantidadeInclusa { get; set; }
        public decimal PrecoDeltaUnitario { get; set; }
    }

    public class CarrinhoItemState
    {
        public string ItemKey { get; set; } = string.Empty;
        public int ProdutoId { get; set; }
        public string NomeProduto { get; set; } = string.Empty;
        public decimal PrecoBaseProduto { get; set; }
        public decimal PrecoUnitario { get; set; }
        public int Quantidade { get; set; }
        public List<CarrinhoItemOpcaoState> OpcoesSelecionadas { get; set; } = new();
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