namespace Restaurapp.BlazorServer.Models
{
    public class Produto
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public string Secao { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public bool Ativo { get; set; } = true;
        public decimal Preco { get; set; }
        public string? ImagemUrl { get; set; }
        public List<ProdutoOpcaoSecao> OpcoesSecoes { get; set; } = new();
    }
}