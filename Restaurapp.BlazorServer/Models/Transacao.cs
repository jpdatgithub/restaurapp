using Microsoft.OpenApi;

namespace Restaurapp.BlazorServer.Models
{
    public enum CategoriaDeTransacao
    {
        [Display("Receita")]
        Receita,
        [Display("Despesa")]
        Despesa
    }
    public class Transacao
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataDeCadastro { get; set; }
        public DateTime? DataRetroativa { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public CategoriaDeTransacao Categoria { get; set; }
    }
}