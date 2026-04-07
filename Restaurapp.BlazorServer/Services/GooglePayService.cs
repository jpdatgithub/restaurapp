using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Restaurapp.BlazorServer.Services
{
    public sealed class GooglePayService
    {
        private readonly PedidoService _pedidoService;
        private readonly GooglePayOptions _options;
        private readonly ILogger<GooglePayService> _logger;

        public GooglePayService(
            PedidoService pedidoService,
            IOptions<GooglePayOptions> options,
            ILogger<GooglePayService> logger)
        {
            _pedidoService = pedidoService;
            _options = options.Value;
            _logger = logger;
        }

        public GooglePayClientConfig ObterConfiguracaoCliente(decimal total)
        {
            return new GooglePayClientConfig
            {
                Environment = string.Equals(_options.Environment, "PRODUCTION", StringComparison.OrdinalIgnoreCase)
                    ? "PRODUCTION"
                    : "TEST",
                MerchantName = string.IsNullOrWhiteSpace(_options.MerchantName)
                    ? "Restaurapp Teste"
                    : _options.MerchantName,
                MerchantId = string.IsNullOrWhiteSpace(_options.MerchantId)
                    ? null
                    : _options.MerchantId,
                Gateway = string.IsNullOrWhiteSpace(_options.Gateway)
                    ? "example"
                    : _options.Gateway,
                GatewayMerchantId = string.IsNullOrWhiteSpace(_options.GatewayMerchantId)
                    ? "exampleGatewayMerchantId"
                    : _options.GatewayMerchantId,
                CountryCode = string.IsNullOrWhiteSpace(_options.CountryCode)
                    ? "BR"
                    : _options.CountryCode.ToUpperInvariant(),
                CurrencyCode = string.IsNullOrWhiteSpace(_options.CurrencyCode)
                    ? "BRL"
                    : _options.CurrencyCode.ToUpperInvariant(),
                ButtonColor = string.IsNullOrWhiteSpace(_options.ButtonColor)
                    ? "black"
                    : _options.ButtonColor.ToLowerInvariant(),
                TotalPrice = total <= 0
                    ? "0.01"
                    : total.ToString("0.00", CultureInfo.InvariantCulture)
            };
        }

        public async Task<GooglePayProcessResult> ProcessarPagamentoTesteAsync(int contaMesaId, decimal total, string token)
        {
            var validacao = ValidarPagamento(total, token);
            if (validacao is not null)
            {
                return validacao;
            }

            if (contaMesaId <= 0)
            {
                return new GooglePayProcessResult(false, "Dados inválidos para processar o pagamento.");
            }

            var fechou = await _pedidoService.FecharContaMesaAsync(contaMesaId);
            if (!fechou)
            {
                return new GooglePayProcessResult(false, "Nao foi possivel finalizar a conta selecionada.");
            }

            var referencia = $"GPAY-TEST-{contaMesaId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _logger.LogInformation(
                "Pagamento Google Pay de teste aprovado para a conta {ContaMesaId} no valor {Valor}. Ref: {Referencia}",
                contaMesaId,
                total,
                referencia);

            return new GooglePayProcessResult(true, "Pagamento aprovado em ambiente de teste.", referencia);
        }

        public Task<GooglePayProcessResult> ProcessarPagamentoCheckoutTesteAsync(int empresaId, decimal total, string token)
        {
            var validacao = ValidarPagamento(total, token);
            if (validacao is not null)
            {
                return Task.FromResult(validacao);
            }

            if (empresaId <= 0)
            {
                return Task.FromResult(new GooglePayProcessResult(false, "Empresa inválida para processar o pagamento."));
            }

            var referencia = $"GPAY-CHK-{empresaId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _logger.LogInformation(
                "Pagamento Google Pay de checkout aprovado para a empresa {EmpresaId} no valor {Valor}. Ref: {Referencia}",
                empresaId,
                total,
                referencia);

            return Task.FromResult(new GooglePayProcessResult(true, "Pagamento aprovado em ambiente de teste.", referencia));
        }

        private GooglePayProcessResult? ValidarPagamento(decimal total, string token)
        {
            if (!_options.Enabled)
            {
                return new GooglePayProcessResult(false, "Google Pay está desabilitado nas configurações.");
            }

            if (total <= 0)
            {
                return new GooglePayProcessResult(false, "Dados inválidos para processar o pagamento.");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return new GooglePayProcessResult(false, "O token do Google Pay não foi recebido.");
            }

            var aceitarQualquerTokenDeTeste = string.Equals(_options.Environment, "TEST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(_options.Gateway, "example", StringComparison.OrdinalIgnoreCase);

            if (!aceitarQualquerTokenDeTeste)
            {
                try
                {
                    using var _ = JsonDocument.Parse(token);
                }
                catch (JsonException)
                {
                    return new GooglePayProcessResult(false, "O token retornado pelo Google Pay está em formato inválido.");
                }
            }

            return null;
        }
    }
}
