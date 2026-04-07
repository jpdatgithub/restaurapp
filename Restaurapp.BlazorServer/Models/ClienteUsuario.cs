namespace Restaurapp.BlazorServer.Models
{
    public class ClienteUsuario
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;
        public DateTime DataCriacaoUtc { get; set; }
    }
}