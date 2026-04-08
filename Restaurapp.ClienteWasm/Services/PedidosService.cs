using Restaurapp.ClienteWasm.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Restaurapp.ClienteWasm.Services
{
    public class PedidosService
    {
        private readonly HttpClient _httpClient;
        private readonly ClienteAuthService _authService;

        public PedidosService(HttpClient httpClient, ClienteAuthService authService)
        {
            _httpClient = httpClient;
            _authService = authService;
        }

        public async Task<(bool Success, PedidoDto? Pedido, string? ErrorMessage)> CheckoutAsync(CheckoutPedidoRequest request)
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, null, "Não foi possível iniciar a sessão do cliente.");
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/pedidos/checkout")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, null, string.IsNullOrWhiteSpace(error) ? "Não foi possível finalizar o pedido." : error);
            }

            var pedido = await response.Content.ReadFromJsonAsync<PedidoDto>();
            if (pedido is null)
            {
                return (false, null, "Resposta inválida ao finalizar pedido.");
            }

            return (true, pedido, null);
        }


        public async Task<(bool Success, GooglePayConfigResponse? Config, string? ErrorMessage)> GetGooglePayConfigCheckoutAsync(int empresaId, decimal total)
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, null, "Não foi possível iniciar a sessão do cliente.");
            }

            var totalParam = Uri.EscapeDataString(total.ToString(CultureInfo.InvariantCulture));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/pedidos/checkout/googlepay/config?empresaId={empresaId}&total={totalParam}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, null, ExtrairMensagemErro(error, "Não foi possível carregar o Google Pay."));
            }

            var config = await response.Content.ReadFromJsonAsync<GooglePayConfigResponse>();
            return config is null
                ? (false, null, "Resposta inválida ao carregar o Google Pay.")
                : (true, config, null);
        }

        public async Task<(bool Success, GooglePayProcessResponse? Response, string? ErrorMessage)> ProcessarPagamentoGooglePayCheckoutAsync(int empresaId, decimal total, string tokenPagamento)
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, null, "Não foi possível iniciar a sessão do cliente.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/pedidos/checkout/googlepay/processar")
            {
                Content = JsonContent.Create(new ProcessarPagamentoGooglePayRequest
                {
                    EmpresaId = empresaId,
                    Total = total,
                    Token = tokenPagamento
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, null, ExtrairMensagemErro(error, "Não foi possível processar o pagamento com Google Pay."));
            }

            var resultado = await response.Content.ReadFromJsonAsync<GooglePayProcessResponse>();
            return resultado is null
                ? (false, null, "Resposta inválida ao processar o pagamento.")
                : (true, resultado, null);
        }

        public async Task<List<PedidoResumoDto>> GetMeusPedidosAsync()
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return new List<PedidoResumoDto>();
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/pedidos/meus");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new List<PedidoResumoDto>();
            }

            var pedidos = await response.Content.ReadFromJsonAsync<List<PedidoResumoDto>>() ?? new List<PedidoResumoDto>();
            return pedidos
                .Where(p => p.TipoAtendimento != TipoAtendimentoPedido.ComerAqui)
                .ToList();
        }

        public async Task<PedidoDto?> GetPedidoAsync(int pedidoId)
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/pedidos/{pedidoId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var pedido = await response.Content.ReadFromJsonAsync<PedidoDto>();
            return pedido?.TipoAtendimento == TipoAtendimentoPedido.ComerAqui ? null : pedido;
        }

        public async Task<(bool Success, PedidoDto? Pedido, string? ErrorMessage)> ConfirmarEntregaAsync(int pedidoId)
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, null, "Não foi possível iniciar a sessão do cliente.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/pedidos/{pedidoId}/confirmar-entrega");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, null, string.IsNullOrWhiteSpace(error) ? "Não foi possível confirmar a entrega." : error);
            }

            var pedido = await response.Content.ReadFromJsonAsync<PedidoDto>();
            return pedido is null
                ? (false, null, "Resposta inválida ao confirmar entrega.")
                : (true, pedido, null);
        }

        private static string ExtrairMensagemErro(string? error, string fallback)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return fallback;
            }

            try
            {
                using var document = JsonDocument.Parse(error);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("message", out var messageProperty) && messageProperty.ValueKind == JsonValueKind.String)
                    {
                        return messageProperty.GetString() ?? fallback;
                    }

                    if (root.TryGetProperty("mensagem", out var mensagemProperty) && mensagemProperty.ValueKind == JsonValueKind.String)
                    {
                        return mensagemProperty.GetString() ?? fallback;
                    }
                }
                else if (root.ValueKind == JsonValueKind.String)
                {
                    return root.GetString() ?? fallback;
                }
            }
            catch
            {
                // Mantém o fallback para respostas que não estão em JSON válido.
            }

            return error;
        }
    }
}