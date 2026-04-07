namespace Restaurapp.BlazorServer.Models
{
    public class Empresa
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public bool HabilitarContasPosPagas { get; set; }
    }
}
