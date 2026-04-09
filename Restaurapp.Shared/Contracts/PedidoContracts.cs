namespace Restaurapp.Shared.Contracts;

public enum TipoAtendimentoPedido
{
    Entrega = 0,
    Retirada = 1,
    ComerAqui = 2
}

public enum OrigemPedido
{
    AplicativoClienteAutenticado = 0,
    QrCodeClienteAutenticado = 1,
    QrCodeConvidado = 2
}

public enum StatusContaMesa
{
    Aberta = 0,
    Fechada = 1
}

public enum StatusPedido
{
    Criado = 0,
    Confirmado = 1,
    EmPreparacao = 2,
    Enviado = 3,
    Concluido = 4,
    Cancelado = 5,
    VendaAvulsa = 6
}

public class CheckoutPedidoRequest
{
    public int EmpresaId { get; set; }
    public bool Takeaway { get; set; }
    public string? NumeroMesa { get; set; }
    public string? EnderecoEntrega { get; set; }
    public string? Observacoes { get; set; }
    public List<CheckoutPedidoItemRequest> Itens { get; set; } = new();
}

public class CheckoutPedidoItemOpcaoRequest
{
    public int ProdutoOpcaoId { get; set; }
    public int Quantidade { get; set; }
}

public class CheckoutPedidoItemRequest
{
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public List<CheckoutPedidoItemOpcaoRequest> Opcoes { get; set; } = new();
}

public class ItemDePedidoOpcaoDto
{
    public string NomeSecao { get; set; } = string.Empty;
    public string NomeOpcao { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitarioDelta { get; set; }
    public decimal SubtotalDelta { get; set; }
}

public class ItemDePedidoDto
{
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = string.Empty;
    public decimal PrecoUnitario { get; set; }
    public int Quantidade { get; set; }
    public decimal Subtotal { get; set; }
    public List<ItemDePedidoOpcaoDto> Opcoes { get; set; } = new();
}

public class PedidoDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = string.Empty;
    public int ClienteUsuarioId { get; set; }
    public int? ContaMesaId { get; set; }
    public string NomeCliente { get; set; } = string.Empty;
    public string EmailCliente { get; set; } = string.Empty;
    public StatusPedido Status { get; set; }
    public bool Takeaway { get; set; }
    public string? NumeroMesa { get; set; }
    public TipoAtendimentoPedido TipoAtendimento { get; set; }
    public OrigemPedido Origem { get; set; }
    public string Moeda { get; set; } = "BRL";
    public decimal Subtotal { get; set; }
    public decimal Desconto { get; set; }
    public decimal Frete { get; set; }
    public decimal Total { get; set; }
    public string? EnderecoEntrega { get; set; }
    public string? Observacoes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<ItemDePedidoDto> Itens { get; set; } = new();
    public List<HistoricoStatusPedidoDto> HistoricoStatus { get; set; } = new();
}

public class PedidoResumoDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = string.Empty;
    public int ClienteUsuarioId { get; set; }
    public int? ContaMesaId { get; set; }
    public string NomeCliente { get; set; } = string.Empty;
    public StatusPedido Status { get; set; }
    public bool Takeaway { get; set; }
    public string? NumeroMesa { get; set; }
    public TipoAtendimentoPedido TipoAtendimento { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int QuantidadeItens { get; set; }
}

public class HistoricoStatusPedidoDto
{
    public StatusPedido Status { get; set; }
    public DateTime DataStatusUtc { get; set; }
    public string RegistradoPor { get; set; } = string.Empty;
}

public class EventoPedidoTempoRealDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int ClienteUsuarioId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataAtualizacaoUtc { get; set; }
}

public class ContaAbertaResumoDto
{
    public int ContaMesaId { get; set; }
    public int EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = string.Empty;
    public string NumeroMesa { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public decimal TotalConta { get; set; }
}

public class ContaAbertaDetalheDto
{
    public int ContaMesaId { get; set; }
    public int EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = string.Empty;
    public string NumeroMesa { get; set; } = string.Empty;
    public StatusContaMesa Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public decimal TotalConta { get; set; }
    public List<ContaPedidoDto> Pedidos { get; set; } = new();
}

public class ContaPedidoDto
{
    public int PedidoId { get; set; }
    public StatusPedido Status { get; set; }
    public TipoAtendimentoPedido TipoAtendimento { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public decimal Total { get; set; }
    public List<ContaPedidoItemDto> Itens { get; set; } = new();
}

public class ContaPedidoItemDto
{
    public string NomeProduto { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public List<ItemDePedidoOpcaoDto> Opcoes { get; set; } = new();
}

public class GooglePayConfigResponse
{
    public string Environment { get; set; } = "TEST";
    public string MerchantName { get; set; } = "Restaurapp Teste";
    public string? MerchantId { get; set; }
    public string Gateway { get; set; } = "example";
    public string GatewayMerchantId { get; set; } = "exampleGatewayMerchantId";
    public string CountryCode { get; set; } = "BR";
    public string CurrencyCode { get; set; } = "BRL";
    public string ButtonColor { get; set; } = "black";
    public string TotalPrice { get; set; } = "0.00";
}

public class ProcessarPagamentoGooglePayRequest
{
    public string Token { get; set; } = string.Empty;
    public int? EmpresaId { get; set; }
    public decimal? Total { get; set; }
}

public class GooglePayProcessResponse
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public string? Referencia { get; set; }
}
