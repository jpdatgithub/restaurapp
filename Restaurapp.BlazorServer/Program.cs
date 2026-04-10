using MudBlazor.Services;
using Restaurapp.BlazorServer.Components;
using Restaurapp.BlazorServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Restaurapp.BlazorServer.Services;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Authorization;
using Serilog;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Text;
using Restaurapp.BlazorServer.Hubs;
using System.Globalization;

// 1. Configuração inicial do Logger (Bootstrap) para capturar erros de inicialização
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var culture = new CultureInfo("pt-BR");
    CultureInfo.DefaultThreadCurrentCulture = culture;
    CultureInfo.DefaultThreadCurrentUICulture = culture;

    var builder = WebApplication.CreateBuilder(args);

    // Configuração do Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var infoVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var logsDir = Path.Combine(context.HostingEnvironment.ContentRootPath, "logs");

        // Tenta criar o diretório, mas não trava se falhar (importante para Docker)
        try { Directory.CreateDirectory(logsDir); } catch { }

        var logFilePath = Path.Combine(logsDir, $"arqsmb-v{infoVersion}-{timestamp}.log");

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console() // Prioridade máxima para ver no Docker logs
            .WriteTo.File(logFilePath, shared: true, flushToDiskInterval: TimeSpan.FromSeconds(1));
    });

    // --- Início das Configurações de Serviços ---
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddDbContextFactory<AppDbContext>((serviceProvider, options) =>
    {
        options.UseNpgsql(connectionString);
    }, ServiceLifetime.Scoped);

    builder.Services.AddScoped<AppDbContext>(serviceProvider =>
    {
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        return factory.CreateDbContext();
    });

    builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

    var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key não configurada.");
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Restaurapp.BlazorServer";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Restaurapp.ClienteWasm";
    var trustAllForwardedHeaders = builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAll");

    builder.Services.AddAuthentication()
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/pedidos"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });

    builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ClienteWasm", policy =>
        {
            policy.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true);
        });
    });

    builder.Services.Configure<UploadsOptions>(builder.Configuration.GetSection(UploadsOptions.SectionName));
    builder.Services.Configure<GooglePayOptions>(builder.Configuration.GetSection(GooglePayOptions.SectionName));
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            if (trustAllForwardedHeaders)
            {
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            }
        });

    builder.Services.AddScoped<IProvedorDeTenantService, ProvedorDeTenantService>();
    builder.Services.AddScoped<IMagicRegisterLinkService, MagicRegisterLinkService>();
    builder.Services.AddScoped<TransacaoService>();
    builder.Services.AddScoped<ProdutoService>();
    builder.Services.AddScoped<PedidoService>();
    builder.Services.AddScoped<GooglePayService>();
    builder.Services.AddScoped<IImagemPublicaService, ImagemPublicaService>();
    builder.Services.AddScoped<ServicoWorkflowPedido>();
    builder.Services.AddScoped<ServicoTempoRealPedidos>();
    builder.Services.AddScoped<ServicoResetBancoDados>();
    builder.Services.AddSingleton<CanalAtualizacaoPedidos>();
    builder.Services.AddSingleton<IProdutoImagemStorage, ProdutoImagemStorage>();
    builder.Services.AddMudServices();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSignalR();
    builder.Services.AddControllers();
    builder.Services.AddRazorComponents().AddInteractiveServerComponents();

    var app = builder.Build();

    // --- BLOCO DE MIGRAÇÃO CORRIGIDO ---
    if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
    {
        Log.Information(">>> Iniciando migrações do banco de dados...");
        using var scope = app.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        try
        {
            await db.Database.MigrateAsync();
            Log.Information(">>> Migrações aplicadas com sucesso!");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, ">>> Falha crítica na migração do banco de dados!");
            throw; // Lança para o container fechar com erro e o GitHub Actions parar
        }
        finally
        {
            await Log.CloseAndFlushAsync(); // Garante que o log apareça no console antes de fechar
        }
        return;
    }

    // --- Restante do Pipeline HTTP ---
    var uploadsOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<UploadsOptions>>().Value;
    var uploadsRootPath = Path.IsPathRooted(uploadsOptions.RootPath)
        ? uploadsOptions.RootPath
        : Path.Combine(app.Environment.ContentRootPath, uploadsOptions.RootPath);

    Directory.CreateDirectory(uploadsRootPath);

    app.UseForwardedHeaders();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRootPath),
        RequestPath = "/uploads"
    });

    app.UseSerilogRequestLogging();
    app.UseCors("ClienteWasm");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
    app.MapControllers();
    app.MapHub<HubPedidosTempoReal>("/hubs/pedidos");
    app.MapStaticAssets();
    app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "O aplicativo encerrou inesperadamente.");
}
finally
{
    Log.CloseAndFlush();
}
