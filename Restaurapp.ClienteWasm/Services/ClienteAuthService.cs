using System.Net.Http.Headers;
using System.Net.Http.Json;
using Restaurapp.ClienteWasm.Models;

namespace Restaurapp.ClienteWasm.Services
{
    public class ClienteAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ClienteAuthStateProvider _authStateProvider;

        public ClienteAuthService(HttpClient httpClient, ClienteAuthStateProvider authStateProvider)
        {
            _httpClient = httpClient;
            _authStateProvider = authStateProvider;
        }

        public async Task<(bool Success, string? ErrorMessage)> RegisterAsync(RegisterClienteRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("api/cliente-auth/register", request);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrWhiteSpace(error) ? "Não foi possível registrar." : error);
        }

        public async Task<(bool Success, string? ErrorMessage)> LoginAsync(LoginClienteRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("api/cliente-auth/login", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(error) ? "Credenciais inválidas." : error);
            }

            var data = await response.Content.ReadFromJsonAsync<LoginClienteResponse>();
            if (data is null || string.IsNullOrWhiteSpace(data.Token))
            {
                return (false, "Resposta de login inválida.");
            }

            await _authStateProvider.MarkUserAsAuthenticatedAsync(data.Token);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> ContinueAsGuestAsync(string? nome = null)
        {
            var response = await _httpClient.PostAsJsonAsync("api/cliente-auth/guest", new GuestClienteRequest
            {
                Nome = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim()
            });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(error) ? "Não foi possível iniciar o modo convidado." : error);
            }

            var data = await response.Content.ReadFromJsonAsync<LoginClienteResponse>();
            if (data is null || string.IsNullOrWhiteSpace(data.Token))
            {
                return (false, "Resposta inválida ao iniciar o modo convidado.");
            }

            await _authStateProvider.MarkUserAsAuthenticatedAsync(data.Token);
            return (true, null);
        }

        public async Task<string?> EnsureSessionAsync()
        {
            var token = await _authStateProvider.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            var guestResult = await ContinueAsGuestAsync();
            if (!guestResult.Success)
            {
                return null;
            }

            return await _authStateProvider.GetTokenAsync();
        }

        public Task LogoutAsync()
        {
            return _authStateProvider.MarkUserAsLoggedOutAsync();
        }

        public async Task<ClienteProfileResponse?> GetProfileAsync()
        {
            var token = await EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/cliente-auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ClienteProfileResponse>();
        }

        public async Task<List<EmpresaPublicaDto>> GetEmpresasAsync()
        {
            var empresas = await _httpClient.GetFromJsonAsync<List<EmpresaPublicaDto>>("api/public/empresas");
            return empresas ?? new List<EmpresaPublicaDto>();
        }

        public async Task<EmpresaCatalogoDto?> GetEmpresaCatalogoAsync(int empresaId)
        {
            return await _httpClient.GetFromJsonAsync<EmpresaCatalogoDto>($"api/public/empresas/{empresaId}");
        }
    }
}
