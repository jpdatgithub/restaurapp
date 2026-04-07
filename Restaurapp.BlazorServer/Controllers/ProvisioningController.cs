using Microsoft.AspNetCore.Mvc;
using Restaurapp.BlazorServer.Services;

namespace Restaurapp.BlazorServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProvisioningController : ControllerBase
    {
        private readonly IMagicRegisterLinkService _magicLinkService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProvisioningController> _logger;

        public ProvisioningController(
            IMagicRegisterLinkService magicLinkService,
            IConfiguration configuration,
            ILogger<ProvisioningController> logger)
        {
            _magicLinkService = magicLinkService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("generate-magic-link")]
        public async Task<IActionResult> GenerateMagicLink([FromBody] GenerateMagicLinkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ProvisioningSecret))
            {
                return BadRequest(new { error = "Provisioning secret é obrigatória" });
            }

            // Valida a provisioning secret
            var configuredSecret = _configuration["Provisioning:Secret"]
                ?? Environment.GetEnvironmentVariable("PROVISIONING_SECRET");

            if (string.IsNullOrWhiteSpace(configuredSecret))
            {
                _logger.LogError("Provisioning secret não configurada no sistema");
                return StatusCode(500, new { error = "Sistema de provisioning não configurado" });
            }

            if (request.ProvisioningSecret != configuredSecret)
            {
                _logger.LogWarning("Tentativa de acesso com provisioning secret inválida");
                return Unauthorized(new { error = "Provisioning secret inválida" });
            }

            // Gera o magic link
            var durationInMinutes = request.DurationInMinutes ?? 60; // Padrão: 60 minutos
            var magicLink = await _magicLinkService.GenerateRegisterLinkAsync(durationInMinutes, "Admin");

            _logger.LogInformation("Magic link gerado via API de provisioning");

            return Ok(new
            {
                magicLink = magicLink,
                expiresAt = DateTime.UtcNow.AddMinutes(durationInMinutes)
            });
        }
    }

    public class GenerateMagicLinkRequest
    {
        public string ProvisioningSecret { get; set; } = string.Empty;
        public int? DurationInMinutes { get; set; }
    }
}
