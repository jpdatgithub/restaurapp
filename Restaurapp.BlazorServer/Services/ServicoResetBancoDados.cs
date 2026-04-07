using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public class ServicoResetBancoDados
    {
        private static readonly string[] TabelasPreservadas =
        {
            "__EFMigrationsHistory",
            "AspNetRoles",
            "AspNetRoleClaims"
        };

        private readonly AppDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ServicoResetBancoDados> _logger;

        public ServicoResetBancoDados(
            AppDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<ServicoResetBancoDados> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public async Task ResetAsync(bool confirm, bool allowProduction = false)
        {
            if (!confirm)
            {
                throw new InvalidOperationException(
                    "Operação cancelada. Use o argumento --confirm para limpar os dados da base.");
            }

            if (_environment.IsProduction() && !allowProduction)
            {
                throw new InvalidOperationException(
                    "Ambiente de produção detectado. Use também --allow-production se quiser continuar conscientemente.");
            }

            var empresaNome = ObterConfiguracao("ResetSeed:EmpresaNome", "Empresa Teste");
            var adminNome = ObterConfiguracao("ResetSeed:AdminNome", "Usuário de Teste");
            var adminEmail = ObterConfiguracao("ResetSeed:AdminEmail", "teste@restaurapp.local");
            var adminPassword = ObterConfiguracao("ResetSeed:AdminPassword", "Restaurapp@Teste123!");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            _logger.LogWarning("Iniciando reset de dados do banco no ambiente {EnvironmentName}", _environment.EnvironmentName);

            await LimparDadosAsync();
            await GarantirRolesAsync();

            var empresa = new Empresa
            {
                Nome = empresaNome
            };

            _dbContext.Empresas.Add(empresa);
            await _dbContext.SaveChangesAsync();

            var usuario = new ApplicationUser
            {
                Nome = adminNome,
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                EmpresaId = empresa.Id
            };

            var createResult = await _userManager.CreateAsync(usuario, adminPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Falha ao criar usuário padrão: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }

            var roleResult = await _userManager.AddToRoleAsync(usuario, "Admin");
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Falha ao associar role Admin: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }

            var claimResult = await _userManager.AddClaimAsync(usuario, new Claim("EmpresaId", empresa.Id.ToString()));
            if (!claimResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Falha ao adicionar claim EmpresaId: {string.Join(", ", claimResult.Errors.Select(e => e.Description))}");
            }

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Reset concluído com sucesso. Empresa seed: {EmpresaNome}. Usuário seed: {AdminEmail}",
                empresaNome,
                adminEmail);

            Console.WriteLine("Reset concluído com sucesso.");
            Console.WriteLine($"Empresa seed: {empresaNome}");
            Console.WriteLine($"Usuário seed: {adminEmail}");
        }

        private async Task LimparDadosAsync()
        {
            var tabelasPreservadasSql = string.Join(", ", TabelasPreservadas.Select(t => $"'{t}'"));

            var sql = $@"
DO $$
DECLARE
    tabelas text;
BEGIN
    SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
      INTO tabelas
      FROM pg_tables
     WHERE schemaname = 'public'
       AND tablename NOT IN ({tabelasPreservadasSql});

    IF tabelas IS NOT NULL THEN
        EXECUTE 'TRUNCATE TABLE ' || tabelas || ' RESTART IDENTITY CASCADE';
    END IF;
END $$;";

            await _dbContext.Database.ExecuteSqlRawAsync(sql);
        }

        private async Task GarantirRolesAsync()
        {
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole("Admin"));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Falha ao recriar role Admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            if (!await _roleManager.RoleExistsAsync("Regular"))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole("Regular"));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Falha ao recriar role Regular: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }

        private string ObterConfiguracao(string chave, string valorPadrao)
        {
            var valor = _configuration[chave];
            return string.IsNullOrWhiteSpace(valor) ? valorPadrao : valor.Trim();
        }
    }
}
