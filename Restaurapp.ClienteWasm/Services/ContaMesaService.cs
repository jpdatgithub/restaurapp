using Restaurapp.ClienteWasm.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Restaurapp.ClienteWasm.Services
{
    public class ContaMesaService
    {
        private readonly HttpClient _httpClient;
        private readonly ClienteAuthService _authService;

        public ContaMesaService(HttpClient httpClient, ClienteAuthService authService)
        {
            _httpClient = httpClient;
            _authService = authService;
        }

        public async Task<ContaAbertaResumoDto?> ObterContaAbertaResumoAsync()
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/pedidos/conta-aberta");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ContaAbertaResumoDto>();
        }

        public async Task<ContaAbertaDetalheDto?> ObterContaAbertaDetalheAsync()
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/pedidos/conta-aberta/detalhe");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ContaAbertaDetalheDto>();
        }

        public async Task<GooglePayConfigResponse?> ObterGooglePayConfigAsync()
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/pedidos/conta-aberta/googlepay/config");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GooglePayConfigResponse>();
        }

        public async Task<(bool Success, GooglePayProcessResponse? Resultado, string? ErrorMessage)> ProcessarPagamentoGooglePayAsync(string tokenGooglePay)
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, null, "Sessão inválida.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/pedidos/conta-aberta/googlepay/processar")
            {
                Content = JsonContent.Create(new ProcessarPagamentoGooglePayRequest
                {
                    Token = tokenGooglePay
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
                mensagem = response.StatusCode == HttpStatusCode.NotFound
                    ? "Você não possui conta aberta."
                    : "Não foi possível processar o pagamento com Google Pay.";
            }

            return (false, resultado, mensagem);
        }

        public async Task<(bool Success, string? ErrorMessage)> PagarContaAbertaAsync()
        {
            var token = await _authService.EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "Sessão inválida.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/pedidos/conta-aberta/pagar");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var mensagem = response.StatusCode == HttpStatusCode.NotFound
                ? "Você não possui conta aberta."
                : "Não foi possível finalizar a conta agora.";

            return (false, mensagem);
        }
    }
}
