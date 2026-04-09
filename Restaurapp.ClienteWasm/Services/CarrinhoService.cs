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
                        ItemKey = i.ItemKey,
                        ProdutoId = i.ProdutoId,
                        NomeProduto = i.NomeProduto,
                        PrecoBaseProduto = i.PrecoBaseProduto,
                        PrecoUnitario = i.PrecoUnitario,
                        Quantidade = i.Quantidade,
                        OpcoesSelecionadas = i.OpcoesSelecionadas
                            .Select(CloneOpcao)
                            .ToList()
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
            decimal precoBaseProduto,
            int quantidade = 1,
            List<CarrinhoItemOpcaoState>? opcoesSelecionadas = null)
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

            var opcoesNormalizadas = (opcoesSelecionadas ?? new List<CarrinhoItemOpcaoState>())
                .Where(o => o.ProdutoOpcaoId > 0 && o.Quantidade > 0)
                .OrderBy(o => o.ProdutoOpcaoId)
                .Select(CloneOpcao)
                .ToList();

            var itemKey = ConstruirItemKey(produtoId, opcoesNormalizadas);
            var precoUnitarioFinal = CalcularPrecoUnitarioFinal(precoBaseProduto, opcoesNormalizadas);

            var itemExistente = _state.Itens.FirstOrDefault(i => i.ItemKey == itemKey);
            if (itemExistente is null)
            {
                _state.Itens.Add(new CarrinhoItemState
                {
                    ItemKey = itemKey,
                    ProdutoId = produtoId,
                    NomeProduto = nomeProduto,
                    PrecoBaseProduto = precoBaseProduto,
                    PrecoUnitario = precoUnitarioFinal,
                    Quantidade = quantidade,
                    OpcoesSelecionadas = opcoesNormalizadas
                });
            }
            else
            {
                itemExistente.Quantidade += quantidade;
            }

            await SaveAsync();
            return (true, null);
        }

        public Task UpdateQuantidadeAsync(int produtoId, int quantidade)
        {
            return UpdateQuantidadeAsync(produtoId.ToString(), quantidade, produtoId);
        }

        public async Task UpdateQuantidadeAsync(string itemKey, int quantidade, int? produtoIdFallback = null)
        {
            await EnsureLoadedAsync();

            var item = _state.Itens.FirstOrDefault(i => i.ItemKey == itemKey)
                ?? (produtoIdFallback.HasValue ? _state.Itens.FirstOrDefault(i => i.ProdutoId == produtoIdFallback.Value) : null);

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

        public Task RemoveItemAsync(int produtoId)
        {
            return RemoveItemAsync(produtoId.ToString(), produtoId);
        }

        public async Task RemoveItemAsync(string itemKey, int? produtoIdFallback = null)
        {
            await EnsureLoadedAsync();

            var item = _state.Itens.FirstOrDefault(i => i.ItemKey == itemKey)
                ?? (produtoIdFallback.HasValue ? _state.Itens.FirstOrDefault(i => i.ProdutoId == produtoIdFallback.Value) : null);

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
                        NormalizarCarrinhoCarregado();
                    }
                }
            }
            catch
            {
                _state = new CarrinhoState();
            }

            _isLoaded = true;
        }

        private void NormalizarCarrinhoCarregado()
        {
            foreach (var item in _state.Itens)
            {
                item.OpcoesSelecionadas ??= new List<CarrinhoItemOpcaoState>();

                if (item.PrecoBaseProduto <= 0)
                {
                    item.PrecoBaseProduto = item.PrecoUnitario;
                }

                if (string.IsNullOrWhiteSpace(item.ItemKey))
                {
                    item.ItemKey = ConstruirItemKey(item.ProdutoId, item.OpcoesSelecionadas);
                }

                item.PrecoUnitario = CalcularPrecoUnitarioFinal(item.PrecoBaseProduto, item.OpcoesSelecionadas);
            }
        }

        private async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(_state, _jsonOptions);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", CarrinhoStorageKey, json);
        }

        private static CarrinhoItemOpcaoState CloneOpcao(CarrinhoItemOpcaoState opcao) => new()
        {
            ProdutoOpcaoId = opcao.ProdutoOpcaoId,
            NomeSecao = opcao.NomeSecao,
            NomeOpcao = opcao.NomeOpcao,
            Quantidade = opcao.Quantidade,
            QuantidadeInclusa = opcao.QuantidadeInclusa,
            PrecoDeltaUnitario = opcao.PrecoDeltaUnitario
        };

        private static decimal CalcularPrecoUnitarioFinal(decimal precoBaseProduto, IEnumerable<CarrinhoItemOpcaoState>? opcoesSelecionadas)
        {
            var totalOpcoes = opcoesSelecionadas?
                .Where(o => o.ProdutoOpcaoId > 0 && o.Quantidade > 0)
                .Sum(o => o.PrecoDeltaUnitario * Math.Max(0, o.Quantidade - Math.Max(0, o.QuantidadeInclusa))) ?? 0m;

            return precoBaseProduto + totalOpcoes;
        }

        private static string ConstruirItemKey(int produtoId, IEnumerable<CarrinhoItemOpcaoState>? opcoesSelecionadas)
        {
            var opcoes = opcoesSelecionadas?
                .Where(o => o.ProdutoOpcaoId > 0 && o.Quantidade > 0)
                .OrderBy(o => o.ProdutoOpcaoId)
                .Select(o => $"{o.ProdutoOpcaoId}:{o.Quantidade}")
                .ToList() ?? new List<string>();

            return opcoes.Count == 0
                ? $"produto:{produtoId}:padrao"
                : $"produto:{produtoId}:{string.Join("|", opcoes)}";
        }
    }
}