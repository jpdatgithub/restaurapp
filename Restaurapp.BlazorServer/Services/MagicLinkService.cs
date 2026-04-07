using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;

namespace Restaurapp.BlazorServer.Services
{
    public interface IMagicRegisterLinkService
    {
        Task<string> GenerateRegisterLinkAsync(int durationInMinutes, string roleToRegister = "Regular", int? empresaId = null);
        Task<bool> ValidateRegisterTokenAsync(string token);
        Task MarkTokenAsUsedAsync(string token);
        Task<int?> GetEmpresaIdFromTokenAsync(string token);
        Task<string> GetRoleToRegisterFromTokenAsync(string token);
    }

    public class MagicRegisterLinkService : IMagicRegisterLinkService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MagicRegisterLinkService(
            AppDbContext context,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> GenerateRegisterLinkAsync(int durationInMinutes, string roleToRegister = "Regular", int? empresaId = null)
        {
            // Gera um token único para registro
            var token = GenerateSecureToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(durationInMinutes);

            var registerToken = new MagicRegisterToken
            {
                Token = token,
                ExpiresAt = expiresAt,
                Used = false,
                EmpresaId = empresaId,
                RoleToRegister = roleToRegister
            };

            _context.MagicRegisterTokens.Add(registerToken);
            await _context.SaveChangesAsync();

            // Retorna a URL completa
            var httpContext = _httpContextAccessor.HttpContext;
            var baseUrl = httpContext != null
                ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}"
                : _configuration["AppUrl"] ?? "http://localhost:5197";
            return $"{baseUrl}/register?token={token}";
        }

        public async Task<bool> ValidateRegisterTokenAsync(string token)
        {
            var registerToken = await _context.MagicRegisterTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.ExpiresAt > DateTime.UtcNow);

            return registerToken != null;
        }

        public async Task MarkTokenAsUsedAsync(string token)
        {
            var registerToken = await _context.MagicRegisterTokens
                .FirstOrDefaultAsync(t => t.Token == token);

            if (registerToken != null)
            {
                registerToken.Used = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int?> GetEmpresaIdFromTokenAsync(string token)
        {
            var registerToken = await _context.MagicRegisterTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.ExpiresAt > DateTime.UtcNow);

            return registerToken?.EmpresaId;
        }

        public async Task<string> GetRoleToRegisterFromTokenAsync(string token)
        {
            var registerToken = await _context.MagicRegisterTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.Used && t.ExpiresAt > DateTime.UtcNow);

            return registerToken?.RoleToRegister ?? string.Empty;
        }

        private string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }

    public class MagicRegisterToken
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? EmpresaId { get; set; }
        public string RoleToRegister { get; set; } = string.Empty;
    }
}
