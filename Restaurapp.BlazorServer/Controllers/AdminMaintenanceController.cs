using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Restaurapp.BlazorServer.Services;

namespace Restaurapp.BlazorServer.Controllers
{
    [ApiController]
    [Route("api/admin/maintenance")]
    public class AdminMaintenanceController : ControllerBase
    {
        private const string SecretHeaderName = "X-Admin-Reset-Secret";

        private readonly IConfiguration _configuration;
        private readonly ServicoResetBancoDados _resetService;
        private readonly ILogger<AdminMaintenanceController> _logger;

        public AdminMaintenanceController(
            IConfiguration configuration,
            ServicoResetBancoDados resetService,
            ILogger<AdminMaintenanceController> logger)
        {
            _configuration = configuration;
            _resetService = resetService;
            _logger = logger;
        }

        [HttpPost("reset-data")]
        public async Task<IActionResult> ResetData(
            [FromHeader(Name = SecretHeaderName)] string? secret,
            [FromQuery] bool confirm = true)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                return BadRequest(new { error = $"Informe o header {SecretHeaderName}." });
            }

            var configuredSecret = _configuration["AdminReset:Secret"]
                ?? Environment.GetEnvironmentVariable("AdminReset__Secret");

            if (string.IsNullOrWhiteSpace(configuredSecret))
            {
                _logger.LogError("AdminReset:Secret não está configurado.");
                return StatusCode(500, new { error = "Secret de reset administrativo não configurado." });
            }

            if (!SegredosIguais(configuredSecret, secret))
            {
                _logger.LogWarning("Tentativa de reset com secret administrativo inválido.");
                return Unauthorized(new { error = "Secret administrativo inválido." });
            }

            var resultado = await _resetService.ResetAsync(confirm);

            return Ok(new
            {
                success = true,
                message = "Reset executado com sucesso.",
                empresa = resultado.EmpresaNome,
                admin = resultado.AdminEmail,
                cliente = resultado.ClienteEmail,
                produto = resultado.ProdutoNome
            });
        }

        private static bool SegredosIguais(string esperado, string informado)
        {
            var esperadoBytes = Encoding.UTF8.GetBytes(esperado);
            var informadoBytes = Encoding.UTF8.GetBytes(informado);

            return esperadoBytes.Length == informadoBytes.Length
                && CryptographicOperations.FixedTimeEquals(esperadoBytes, informadoBytes);
        }
    }
}
