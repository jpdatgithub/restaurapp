using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;
using System.Security.Claims;

var services = new ServiceCollection();

services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddFilter("Microsoft", LogLevel.Warning);
    logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
});

var serverProjectPath = Path.Combine(Directory.GetCurrentDirectory(), "Restaurapp.BlazorServer");
if (!Directory.Exists(serverProjectPath))
{
    throw new DirectoryNotFoundException($"Nao foi possivel localizar 'Restaurapp.BlazorServer' em '{Directory.GetCurrentDirectory()}'. Execute o script a partir da raiz do repositorio.");
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(serverProjectPath)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao encontrada nos appsettings do servidor.");
}

services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
services.AddDataProtection();
services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DefaultUsersSeeder");
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

await dbContext.Database.MigrateAsync();

const string serverNome = "teste";
const string serverEmail = "teste@gmail.com";
const string serverSenha = "Teste123@";

const string clienteNome = "uteste";
const string clienteEmail = "uteste@gmail.com";
const string clienteSenha = "Teste123@";

const string adminRole = "Admin";

if (!await roleManager.RoleExistsAsync(adminRole))
{
    var roleCreateResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
    if (!roleCreateResult.Succeeded)
    {
        var errors = string.Join(", ", roleCreateResult.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Nao foi possivel criar role '{adminRole}': {errors}");
    }
}

var empresa = await dbContext.Empresas.FirstOrDefaultAsync(e => e.Nome == "Empresa Seed");
if (empresa is null)
{
    empresa = new Empresa { Nome = "Empresa Seed" };
    dbContext.Empresas.Add(empresa);
    await dbContext.SaveChangesAsync();
}

var serverUser = await userManager.FindByEmailAsync(serverEmail);
if (serverUser is null)
{
    serverUser = new ApplicationUser
    {
        UserName = serverEmail,
        Email = serverEmail,
        Nome = serverNome,
        EmailConfirmed = true,
        EmpresaId = empresa.Id
    };

    var createResult = await userManager.CreateAsync(serverUser, serverSenha);
    if (!createResult.Succeeded)
    {
        var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Nao foi possivel criar usuario de servidor '{serverEmail}': {errors}");
    }

    logger.LogInformation("Usuario de servidor criado: {Email}", serverEmail);
}
else
{
    serverUser.Nome = serverNome;
    serverUser.UserName = serverEmail;
    serverUser.Email = serverEmail;
    serverUser.EmailConfirmed = true;
    serverUser.EmpresaId = empresa.Id;

    var updateResult = await userManager.UpdateAsync(serverUser);
    if (!updateResult.Succeeded)
    {
        var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Nao foi possivel atualizar usuario de servidor '{serverEmail}': {errors}");
    }

    var resetToken = await userManager.GeneratePasswordResetTokenAsync(serverUser);
    var resetResult = await userManager.ResetPasswordAsync(serverUser, resetToken, serverSenha);
    if (!resetResult.Succeeded)
    {
        var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Nao foi possivel resetar senha do usuario de servidor '{serverEmail}': {errors}");
    }

    logger.LogInformation("Usuario de servidor atualizado: {Email}", serverEmail);
}

if (!await userManager.IsInRoleAsync(serverUser, adminRole))
{
    var roleResult = await userManager.AddToRoleAsync(serverUser, adminRole);
    if (!roleResult.Succeeded)
    {
        var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Nao foi possivel adicionar role '{adminRole}' ao usuario '{serverEmail}': {errors}");
    }
}

var claims = await userManager.GetClaimsAsync(serverUser);
var empresaClaim = claims.FirstOrDefault(c => c.Type == "EmpresaId");
if (empresaClaim is null)
{
    await userManager.AddClaimAsync(serverUser, new Claim("EmpresaId", empresa.Id.ToString()));
}
else if (empresaClaim.Value != empresa.Id.ToString())
{
    await userManager.ReplaceClaimAsync(serverUser, empresaClaim, new Claim("EmpresaId", empresa.Id.ToString()));
}

var cliente = await dbContext.ClientesUsuarios.FirstOrDefaultAsync(c => c.Email == clienteEmail);
var passwordHasher = new PasswordHasher<ClienteUsuario>();

if (cliente is null)
{
    cliente = new ClienteUsuario
    {
        Nome = clienteNome,
        Email = clienteEmail,
        DataCriacaoUtc = DateTime.UtcNow
    };
    cliente.SenhaHash = passwordHasher.HashPassword(cliente, clienteSenha);

    dbContext.ClientesUsuarios.Add(cliente);
    await dbContext.SaveChangesAsync();

    logger.LogInformation("Usuario cliente criado: {Email}", clienteEmail);
}
else
{
    cliente.Nome = clienteNome;
    cliente.SenhaHash = passwordHasher.HashPassword(cliente, clienteSenha);
    await dbContext.SaveChangesAsync();

    logger.LogInformation("Usuario cliente atualizado: {Email}", clienteEmail);
}

logger.LogInformation("Seed de usuarios finalizado com sucesso.");
