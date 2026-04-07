using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Restaurapp.ClienteWasm;
using Restaurapp.ClienteWasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ClienteAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ClienteAuthStateProvider>());
builder.Services.AddScoped<ClienteAuthService>();
builder.Services.AddScoped<CarrinhoService>();
builder.Services.AddScoped<PedidosService>();
builder.Services.AddScoped<ContaMesaService>();
builder.Services.AddScoped<ContaMesaEstadoService>();
builder.Services.AddScoped<ServicoTempoRealPedidosCliente>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
