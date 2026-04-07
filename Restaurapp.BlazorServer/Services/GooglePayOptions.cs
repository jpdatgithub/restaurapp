namespace Restaurapp.BlazorServer.Services
{
    public sealed class GooglePayOptions
    {
        public const string SectionName = "GooglePay";

        public bool Enabled { get; set; } = true;
        public string Environment { get; set; } = "TEST";
        public string MerchantName { get; set; } = "Restaurapp Teste";
        public string? MerchantId { get; set; }
        public string Gateway { get; set; } = "example";
        public string GatewayMerchantId { get; set; } = "exampleGatewayMerchantId";
        public string CountryCode { get; set; } = "BR";
        public string CurrencyCode { get; set; } = "BRL";
        public string ButtonColor { get; set; } = "black";
    }

    public sealed class GooglePayClientConfig
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

    public sealed record GooglePayProcessResult(bool Sucesso, string Mensagem, string? Referencia = null);
}
