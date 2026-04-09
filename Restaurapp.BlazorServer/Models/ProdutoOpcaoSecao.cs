namespace Restaurapp.BlazorServer.Models
{
    public class ProdutoOpcaoSecao
    {
        public int Id { get; set; }
        public int ProdutoId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int Ordem { get; set; }
        public int MinSelecoes { get; set; }
        public int MaxSelecoes { get; set; }
        public bool PermitirQuantidade { get; set; } = true;
        public bool Ativa { get; set; } = true;
        public Produto? Produto { get; set; }
        public List<ProdutoOpcao> Opcoes { get; set; } = new();
    }
}