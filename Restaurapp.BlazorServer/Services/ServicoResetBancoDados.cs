using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public sealed record ResultadoResetBancoDados(
        string EmpresaNome,
        string AdminEmail,
        string ClienteEmail,
        string ProdutoNome);

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

        public async Task<ResultadoResetBancoDados> ResetAsync(bool confirm)
        {
            if (!confirm)
            {
                throw new InvalidOperationException(
                    "Operação cancelada. Envie confirm=true para limpar os dados da base.");
            }

            if (_environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Ambiente de produção detectado. O reset remoto está bloqueado por segurança.");
            }

            var empresaNome = ObterConfiguracao("ResetSeed:EmpresaNome", "Empresa Seed");
            var adminNome = ObterConfiguracao("ResetSeed:AdminNome", "teste");
            var adminEmail = ObterConfiguracao("ResetSeed:AdminEmail", "teste@gmail.com");
            var adminPassword = ObterConfiguracao("ResetSeed:AdminPassword", "Teste123@");
            var clienteNome = ObterConfiguracao("ResetSeed:ClienteNome", "uteste");
            var clienteEmail = ObterConfiguracao("ResetSeed:ClienteEmail", "uteste@gmail.com");
            var clientePassword = ObterConfiguracao("ResetSeed:ClientePassword", "Teste123@");
            var produtoSecao = ObterConfiguracao("ResetSeed:ProdutoSecao", "Testes");
            var produtoNome = ObterConfiguracao("ResetSeed:ProdutoNome", "Produto Teste");
            var produtoDescricao = ObterConfiguracao("ResetSeed:ProdutoDescricao", "Criado automaticamente pelo reset para facilitar os testes.");
            var produtoPreco = ObterConfiguracaoDecimal("ResetSeed:ProdutoPreco", 19.90m);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            _logger.LogWarning("Iniciando reset de dados do banco no ambiente {EnvironmentName}", _environment.EnvironmentName);

            await LimparDadosAsync();
            await GarantirRolesAsync();

            var empresa = new Empresa
            {
                Nome = empresaNome,
                HabilitarContasPosPagas = true
            };

            _dbContext.Empresas.Add(empresa);
            await _dbContext.SaveChangesAsync();

            await CriarAdministradorAsync(empresa, adminNome, adminEmail, adminPassword);
            await CriarClientePadraoAsync(clienteNome, clienteEmail, clientePassword);
            await CriarProdutoPadraoAsync(empresa, produtoSecao, produtoNome, produtoDescricao, produtoPreco);

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Reset concluído com sucesso. Empresa: {EmpresaNome}. Admin: {AdminEmail}. Cliente: {ClienteEmail}. Produto: {ProdutoNome}",
                empresaNome,
                adminEmail,
                clienteEmail,
                produtoNome);

            Console.WriteLine("Reset concluído com sucesso.");
            Console.WriteLine($"Empresa seed: {empresaNome}");
            Console.WriteLine($"Admin seed: {adminEmail}");
            Console.WriteLine($"Cliente seed: {clienteEmail}");
            Console.WriteLine($"Produto seed: {produtoNome}");

            return new ResultadoResetBancoDados(empresaNome, adminEmail, clienteEmail, produtoNome);
        }

        private async Task CriarAdministradorAsync(Empresa empresa, string adminNome, string adminEmail, string adminPassword)
        {
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
                    $"Falha ao criar usuário admin padrão: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
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
        }

        private async Task CriarClientePadraoAsync(string clienteNome, string clienteEmail, string clientePassword)
        {
            var cliente = new ClienteUsuario
            {
                Nome = clienteNome,
                Email = clienteEmail,
                DataCriacaoUtc = DateTime.UtcNow
            };

            var passwordHasher = new PasswordHasher<ClienteUsuario>();
            cliente.SenhaHash = passwordHasher.HashPassword(cliente, clientePassword);

            _dbContext.ClientesUsuarios.Add(cliente);
            await _dbContext.SaveChangesAsync();
        }

        private async Task CriarProdutoPadraoAsync(
            Empresa empresa,
            string secao,
            string nome,
            string descricao,
            decimal preco)
        {
            var produto = new Produto
            {
                EmpresaId = empresa.Id,
                Secao = secao,
                Nome = nome,
                Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
                Ativo = true,
                Preco = preco
            };

            _dbContext.Produtos.Add(produto);
            await _dbContext.SaveChangesAsync();
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

        private decimal ObterConfiguracaoDecimal(string chave, decimal valorPadrao)
        {
            var valor = _configuration[chave];
            if (string.IsNullOrWhiteSpace(valor))
            {
                return valorPadrao;
            }

            if (decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var valorConvertido)
                || decimal.TryParse(valor, NumberStyles.Number, new CultureInfo("pt-BR"), out valorConvertido))
            {
                return valorConvertido;
            }

            return valorPadrao;
        }
    }
}
