using Restaurapp.ClienteWasm.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

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

        public async Task<GooglePayConfigResponse?> ObterGooglePayCheckoutConfigAsync(int empresaId, decimal total)
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var totalFormatado = Uri.EscapeDataString(total.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR")));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/pedidos/checkout/googlepay/config?empresaId={empresaId}&total={totalFormatado}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GooglePayConfigResponse>();
        }

        public async Task<(bool Success, GooglePayProcessResponse? Resultado, string? ErrorMessage)> ProcessarPagamentoGooglePayCheckoutAsync(int empresaId, decimal total, string tokenGooglePay)
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, null, "Sessão inválida.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/pedidos/checkout/googlepay/processar")
            {
                Content = JsonContent.Create(new ProcessarPagamentoGooglePayRequest
                {
                    Token = tokenGooglePay,
                    EmpresaId = empresaId,
                    Total = total
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            var resultado = await response.Content.ReadFromJsonAsync<GooglePayProcessResponse>();

            if (response.IsSuccessStatusCode)
            {
                return (true, resultado, null);
            }

            var mensagem = resultado?.Mensagem;
            if (string.IsNullOrWhiteSpace(mensagem))
            {
                mensagem = "Não foi possível processar o pagamento com Google Pay.";
            }

            return (false, resultado, mensagem);
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
    }
}