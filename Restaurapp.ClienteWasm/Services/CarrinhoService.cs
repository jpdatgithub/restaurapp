using Microsoft.JSInterop;
using Restaurapp.ClienteWasm.Models;
using System.Text.Json;

namespace Restaurapp.ClienteWasm.Services
{
    public class CarrinhoService
    {
        private const string CarrinhoStorageKey = "cliente_carrinho";

        private readonly IJSRuntime _jsRuntime;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private CarrinhoState _state = new();
        private bool _isLoaded;

        public CarrinhoService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<CarrinhoSnapshot> GetSnapshotAsync()
        {
            await EnsureLoadedAsync();

            return new CarrinhoSnapshot
            {
                EmpresaId = _state.EmpresaId,
                EmpresaNome = _state.EmpresaNome,
                Itens = _state.Itens
                    .Select(i => new CarrinhoItemState
                    {
                        ProdutoId = i.ProdutoId,
                        NomeProduto = i.NomeProduto,
                        PrecoUnitario = i.PrecoUnitario,
                        Quantidade = i.Quantidade
                    })
                    .ToList(),
                QuantidadeTotalItens = _state.Itens.Sum(i => i.Quantidade),
                Subtotal = _state.Itens.Sum(i => i.PrecoUnitario * i.Quantidade)
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> AddItemAsync(
            int empresaId,
            string empresaNome,
            int produtoId,
            string nomeProduto,
            decimal precoUnitario,
            int quantidade = 1)
        {
            await EnsureLoadedAsync();

            if (empresaId <= 0 || produtoId <= 0 || quantidade <= 0)
            {
                return (false, "Dados do item inválidos.");
            }

            if (_state.Itens.Any() && _state.EmpresaId != empresaId)
            {
                return (false, "Seu carrinho possui itens de outra empresa. Limpe o carrinho para continuar.");
            }

            _state.EmpresaId = empresaId;
            _state.EmpresaNome = empresaNome;

            var itemExistente = _state.Itens.FirstOrDefault(i => i.ProdutoId == produtoId);
            if (itemExistente is null)
            {
                _state.Itens.Add(new CarrinhoItemState
                {
                    ProdutoId = produtoId,
                    NomeProduto = nomeProduto,
                    PrecoUnitario = precoUnitario,
                    Quantidade = quantidade
                });
            }
            else
            {
                itemExistente.Quantidade += quantidade;
            }

            await SaveAsync();
            return (true, null);
        }

        public async Task UpdateQuantidadeAsync(int produtoId, int quantidade)
        {
            await EnsureLoadedAsync();

            var item = _state.Itens.FirstOrDefault(i => i.ProdutoId == produtoId);
            if (item is null)
            {
                return;
            }

            if (quantidade <= 0)
            {
                _state.Itens.Remove(item);
            }
            else
            {
                item.Quantidade = quantidade;
            }

            if (_state.Itens.Count == 0)
            {
                _state.EmpresaId = 0;
                _state.EmpresaNome = string.Empty;
            }

            await SaveAsync();
        }

        public async Task RemoveItemAsync(int produtoId)
        {
            await EnsureLoadedAsync();

            var item = _state.Itens.FirstOrDefault(i => i.ProdutoId == produtoId);
            if (item is null)
            {
                return;
            }

            _state.Itens.Remove(item);

            if (_state.Itens.Count == 0)
            {
                _state.EmpresaId = 0;
                _state.EmpresaNome = string.Empty;
            }

            await SaveAsync();
        }

        public async Task ClearAsync()
        {
            _state = new CarrinhoState();
            await SaveAsync();
        }

        private async Task EnsureLoadedAsync()
        {
            if (_isLoaded)
            {
                return;
            }

            try
            {
                var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", CarrinhoStorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var parsed = JsonSerializer.Deserialize<CarrinhoState>(json, _jsonOptions);
                    if (parsed is not null)
                    {
                        _state = parsed;
                    }
                }
            }
            catch
            {
                _state = new CarrinhoState();
            }

            _isLoaded = true;
        }

        private async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(_state, _jsonOptions);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", CarrinhoStorageKey, json);
        }
    }
}