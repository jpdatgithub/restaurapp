using Microsoft.AspNetCore.SignalR.Client;
using Restaurapp.ClienteWasm.Models;

namespace Restaurapp.ClienteWasm.Services
{
    public class ServicoTempoRealPedidosCliente : IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ClienteAuthStateProvider _authStateProvider;
        private HubConnection? _hubConnection;

        public ServicoTempoRealPedidosCliente(HttpClient httpClient, ClienteAuthStateProvider authStateProvider)
        {
            _httpClient = httpClient;
            _authStateProvider = authStateProvider;
        }

        public async Task IniciarParaClienteAsync(int clienteUsuarioId, Func<EventoPedidoTempoRealDto, Task> aoReceberAtualizacao)
        {
            if (_hubConnection is null)
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(new Uri(_httpClient.BaseAddress!, "hubs/pedidos"), options =>
                    {
                        options.AccessTokenProvider = async () => await _authStateProvider.GetTokenAsync();
                    })
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<EventoPedidoTempoRealDto>("pedido_atualizado", async evento =>
                {
                    if (evento.ClienteUsuarioId != clienteUsuarioId)
                    {
                        return;
                    }

                    await aoReceberAtualizacao(evento);
                });
            }

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
            }

            await _hubConnection.InvokeAsync("EntrarGrupoCliente", clienteUsuarioId);
        }

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection is null)
            {
                return;
            }

            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}