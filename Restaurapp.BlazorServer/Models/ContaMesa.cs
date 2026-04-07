namespace Restaurapp.BlazorServer.Models
{
    public class ContaMesa
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public int ClienteUsuarioId { get; set; }
        public string NumeroMesa { get; set; } = string.Empty;
        public StatusContaMesa Status { get; set; } = StatusContaMesa.Aberta;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? FechadaAtUtc { get; set; }
        public List<Pedido> Pedidos { get; set; } = new();
    }
}