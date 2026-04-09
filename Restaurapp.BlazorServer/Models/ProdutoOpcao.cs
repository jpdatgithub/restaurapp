namespace Restaurapp.BlazorServer.Models
{
    public class ProdutoOpcao
    {
        public int Id { get; set; }
        public int ProdutoOpcaoSecaoId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public decimal PrecoDelta { get; set; }
        public int QuantidadeMin { get; set; }
        public int QuantidadeMax { get; set; } = 1;
        public bool Ativa { get; set; } = true;
        public int Ordem { get; set; }
        public ProdutoOpcaoSecao? Secao { get; set; }
    }
}