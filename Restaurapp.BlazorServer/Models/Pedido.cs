namespace Restaurapp.BlazorServer.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public int ClienteUsuarioId { get; set; }
        public string NomeClienteSnapshot { get; set; } = string.Empty;
        public string EmailClienteSnapshot { get; set; } = string.Empty;
        public int? ContaMesaId { get; set; }
        public TipoAtendimentoPedido TipoAtendimento { get; set; } = TipoAtendimentoPedido.Entrega;
        public OrigemPedido Origem { get; set; } = OrigemPedido.AplicativoClienteAutenticado;
        public string? NumeroMesa { get; set; }
        public StatusPedido Status { get; set; } = StatusPedido.Criado;
        public bool Takeaway { get; set; }
        public string Moeda { get; set; } = "BRL";
        public decimal Subtotal { get; set; }
        public decimal Desconto { get; set; }
        public decimal Frete { get; set; }
        public decimal Total { get; set; }
        public string? EnderecoEntrega { get; set; }
        public string? Observacoes { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public ContaMesa? ContaMesa { get; set; }
        public List<ItemDePedido> Itens { get; set; } = new();
        public List<HistoricoStatusPedido> HistoricoStatus { get; set; } = new();
    }
}