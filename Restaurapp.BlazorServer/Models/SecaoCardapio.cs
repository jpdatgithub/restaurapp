namespace Restaurapp.BlazorServer.Models
{
    public class SecaoCardapio
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int OrdemNoCardapio { get; set; }
        public bool Ativa { get; set; } = true;
    }
}
