using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;
using Restaurapp.BlazorServer.Services;
using Restaurapp.Shared.Contracts;
using System.Security.Claims;

namespace Restaurapp.BlazorServer.Controllers
{
    [ApiController]
    public class PedidosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IProvedorDeTenantService _provedorDeTenant;
        private readonly ServicoWorkflowPedido _servicoWorkflowPedido;
        private readonly ServicoTempoRealPedidos _servicoTempoRealPedidos;
        private readonly GooglePayService _googlePayService;

        public PedidosController(
            AppDbContext context,
            IProvedorDeTenantService provedorDeTenant,
            ServicoWorkflowPedido servicoWorkflowPedido,
            ServicoTempoRealPedidos servicoTempoRealPedidos,
            GooglePayService googlePayService)
        {
            _context = context;
            _provedorDeTenant = provedorDeTenant;
            _servicoWorkflowPedido = servicoWorkflowPedido;
            _servicoTempoRealPedidos = servicoTempoRealPedidos;
            _googlePayService = googlePayService;
        }

        [HttpPost("api/pedidos/checkout")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Checkout([FromBody] CheckoutPedidoRequest request)
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            if (request.EmpresaId <= 0)
            {
                return BadRequest(new { message = "EmpresaId inválido." });
            }

            if (request.Itens.Count == 0)
            {
                return BadRequest(new { message = "O pedido precisa de pelo menos um item." });
            }

            if (request.Itens.Any(i => i.ProdutoId <= 0 || i.Quantidade <= 0))
            {
                return BadRequest(new { message = "ProdutoId e Quantidade devem ser maiores que zero." });
            }

            if (request.Itens.Any(i => i.Opcoes.Any(o => o.ProdutoOpcaoId <= 0 || o.Quantidade <= 0)))
            {
                return BadRequest(new { message = "As opções selecionadas devem informar um ProdutoOpcaoId válido e quantidade maior que zero." });
            }

            if (!string.IsNullOrWhiteSpace(request.NumeroMesa) && request.NumeroMesa.Trim().Length > 50)
            {
                return BadRequest(new { message = "Número da mesa deve ter no máximo 50 caracteres." });
            }

            var numeroMesaNormalizado = string.IsNullOrWhiteSpace(request.NumeroMesa)
                ? null
                : request.NumeroMesa.Trim();

            if (request.Takeaway && numeroMesaNormalizado is not null)
            {
                return BadRequest(new { message = "Pedido takeaway não pode informar número de mesa." });
            }

            if (!request.Takeaway
                && string.IsNullOrWhiteSpace(request.EnderecoEntrega)
                && numeroMesaNormalizado is null)
            {
                return BadRequest(new { message = "Informe endereço de entrega ou número da mesa." });
            }

            var cliente = await _context.ClientesUsuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clienteId.Value);

            if (cliente is null)
            {
                return Unauthorized();
            }

            var empresa = await _context.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == request.EmpresaId);

            if (empresa is null)
            {
                return NotFound(new { message = "Empresa não encontrada." });
            }

            var contasPosPagasHabilitadas = empresa.HabilitarContasPosPagas;

            ContaMesa? contaMesaAberta = null;
            if (contasPosPagasHabilitadas)
            {
                var contasAbertasCliente = await _context.ContasMesa
                    .IgnoreQueryFilters()
                    .Where(c => c.Status == StatusContaMesa.Aberta
                        && (c.ClienteUsuarioId == clienteId.Value
                            || c.Pedidos.Any(p => p.ClienteUsuarioId == clienteId.Value && p.Status != StatusPedido.Cancelado)))
                    .OrderByDescending(c => c.CreatedAtUtc)
                    .ToListAsync();

                if (contasAbertasCliente.Count > 1)
                {
                    return Conflict(new
                    {
                        message = "Foram encontradas múltiplas contas abertas para este usuário. Finalize as contas pendentes antes de continuar."
                    });
                }

                contaMesaAberta = contasAbertasCliente.FirstOrDefault();

                if (contaMesaAberta is not null)
                {
                    if (numeroMesaNormalizado is null)
                    {
                        return BadRequest(new
                        {
                            message = $"Você já está vinculado à conta da mesa {contaMesaAberta.NumeroMesa}. Finalize essa conta antes de fazer outro tipo de pedido."
                        });
                    }

                    if (!string.Equals(contaMesaAberta.NumeroMesa, numeroMesaNormalizado, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new
                        {
                            message = $"Você já está vinculado à mesa {contaMesaAberta.NumeroMesa}. Faça pedidos nessa mesma mesa até o fechamento da conta."
                        });
                    }

                    if (contaMesaAberta.EmpresaId != request.EmpresaId)
                    {
                        return BadRequest(new
                        {
                            message = "Você possui conta aberta em outra empresa. Finalize-a antes de abrir nova conta."
                        });
                    }
                }
            }

            var itensAgrupados = request.Itens
                .Select(i => new
                {
                    ProdutoId = i.ProdutoId,
                    Quantidade = i.Quantidade,
                    Opcoes = (i.Opcoes ?? new List<CheckoutPedidoItemOpcaoRequest>())
                        .Where(o => o.ProdutoOpcaoId > 0 && o.Quantidade > 0)
                        .GroupBy(o => o.ProdutoOpcaoId)
                        .Select(g => new CheckoutPedidoItemOpcaoRequest
                        {
                            ProdutoOpcaoId = g.Key,
                            Quantidade = g.Sum(x => x.Quantidade)
                        })
                        .OrderBy(o => o.ProdutoOpcaoId)
                        .ToList(),
                    ChaveConfiguracao = ConstruirChaveConfiguracao(i.Opcoes)
                })
                .GroupBy(i => new { i.ProdutoId, i.ChaveConfiguracao })
                .Select(g => new
                {
                    ProdutoId = g.Key.ProdutoId,
                    Quantidade = g.Sum(x => x.Quantidade),
                    Opcoes = g.First().Opcoes
                })
                .ToList();

            var produtosIds = itensAgrupados.Select(i => i.ProdutoId).Distinct().ToList();

            var produtos = await _context.Produtos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(p => p.OpcoesSecoes.Where(s => s.Ativa))
                    .ThenInclude(s => s.Opcoes.Where(o => o.Ativa))
                .Where(p => p.EmpresaId == request.EmpresaId && p.Ativo && produtosIds.Contains(p.Id))
                .ToListAsync();

            if (produtos.Count != produtosIds.Count)
            {
                return NotFound(new { message = "Um ou mais produtos não foram encontrados para a empresa informada." });
            }

            var itensDePedido = new List<ItemDePedido>();
            foreach (var item in itensAgrupados)
            {
                var produto = produtos.First(p => p.Id == item.ProdutoId);
                var (itemDePedido, erroValidacao) = CriarItemDePedidoComOpcoes(produto, item.Quantidade, item.Opcoes);

                if (itemDePedido is null)
                {
                    return BadRequest(new { message = erroValidacao ?? $"Não foi possível montar o item do produto {produto.Nome}." });
                }

                itensDePedido.Add(itemDePedido);
            }

            var subtotal = itensDePedido.Sum(i => i.SubtotalItem);
            var desconto = 0m;
            var frete = 0m;
            var total = subtotal - desconto + frete;
            var agoraUtc = DateTime.UtcNow;

            if (numeroMesaNormalizado is not null && contasPosPagasHabilitadas && contaMesaAberta is null)
            {
                contaMesaAberta = new ContaMesa
                {
                    EmpresaId = request.EmpresaId,
                    ClienteUsuarioId = cliente.Id,
                    NumeroMesa = numeroMesaNormalizado,
                    Status = StatusContaMesa.Aberta,
                    CreatedAtUtc = agoraUtc,
                    UpdatedAtUtc = agoraUtc
                };
                _context.ContasMesa.Add(contaMesaAberta);
            }

            if (contaMesaAberta is not null)
            {
                contaMesaAberta.UpdatedAtUtc = agoraUtc;
            }

            var pedido = new Pedido
            {
                EmpresaId = request.EmpresaId,
                ClienteUsuarioId = cliente.Id,
                ContaMesaId = contaMesaAberta?.Id,
                NomeClienteSnapshot = cliente.Nome,
                EmailClienteSnapshot = cliente.Email,
                TipoAtendimento = numeroMesaNormalizado is not null
                    ? TipoAtendimentoPedido.ComerAqui
                    : (request.Takeaway ? TipoAtendimentoPedido.Retirada : TipoAtendimentoPedido.Entrega),
                Origem = OrigemPedido.AplicativoClienteAutenticado,
                NumeroMesa = numeroMesaNormalizado,
                Status = StatusPedido.Criado,
                Takeaway = request.Takeaway,
                Moeda = "BRL",
                Subtotal = subtotal,
                Desconto = desconto,
                Frete = frete,
                Total = total,
                EnderecoEntrega = request.EnderecoEntrega?.Trim(),
                Observacoes = request.Observacoes?.Trim(),
                CreatedAtUtc = agoraUtc,
                UpdatedAtUtc = agoraUtc,
                Itens = itensDePedido
            };

            if (contaMesaAberta is not null)
            {
                pedido.ContaMesa = contaMesaAberta;
            }

            _servicoWorkflowPedido.RegistrarStatusInicial(pedido, "Cliente");

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            await _servicoTempoRealPedidos.NotificarPedidoAtualizadoAsync(pedido);
            if (pedido.ContaMesaId.HasValue)
            {
                await _servicoTempoRealPedidos.NotificarContaAbertaAtualizadaAsync(pedido.ClienteUsuarioId);
            }

            return Ok(await MapearPedidoParaDtoAsync(pedido));
        }

        private static string ConstruirChaveConfiguracao(IEnumerable<CheckoutPedidoItemOpcaoRequest>? opcoes)
        {
            if (opcoes is null)
            {
                return string.Empty;
            }

            return string.Join(
                "|",
                opcoes
                    .Where(o => o.ProdutoOpcaoId > 0 && o.Quantidade > 0)
                    .OrderBy(o => o.ProdutoOpcaoId)
                    .Select(o => $"{o.ProdutoOpcaoId}:{o.Quantidade}"));
        }

        private static (ItemDePedido? Item, string? ErrorMessage) CriarItemDePedidoComOpcoes(
            Produto produto,
            int quantidade,
            IReadOnlyCollection<CheckoutPedidoItemOpcaoRequest> opcoesSelecionadas)
        {
            if (quantidade <= 0)
            {
                return (null, "A quantidade do item precisa ser maior que zero.");
            }

            var secoesAtivas = produto.OpcoesSecoes
                .Where(s => s.Ativa)
                .OrderBy(s => s.Ordem)
                .ToList();

            var opcoesDisponiveis = secoesAtivas
                .SelectMany(s => s.Opcoes.Where(o => o.Ativa).Select(o => new { Secao = s, Opcao = o }))
                .ToDictionary(x => x.Opcao.Id, x => x);

            var opcoesNormalizadas = opcoesSelecionadas
                .Where(o => o.ProdutoOpcaoId > 0 && o.Quantidade > 0)
                .GroupBy(o => o.ProdutoOpcaoId)
                .Select(g => new CheckoutPedidoItemOpcaoRequest
                {
                    ProdutoOpcaoId = g.Key,
                    Quantidade = g.Sum(x => x.Quantidade)
                })
                .ToList();

            foreach (var opcaoSelecionada in opcoesNormalizadas)
            {
                if (!opcoesDisponiveis.TryGetValue(opcaoSelecionada.ProdutoOpcaoId, out var referencia))
                {
                    return (null, $"A opção #{opcaoSelecionada.ProdutoOpcaoId} não pertence ao produto {produto.Nome}.");
                }

                if (!referencia.Secao.PermitirQuantidade && opcaoSelecionada.Quantidade > 1)
                {
                    return (null, $"A seção {referencia.Secao.Nome} não permite quantidade maior que 1 por opção.");
                }

                if (opcaoSelecionada.Quantidade < referencia.Opcao.QuantidadeMin
                    || opcaoSelecionada.Quantidade > referencia.Opcao.QuantidadeMax)
                {
                    return (null, $"A opção {referencia.Opcao.Nome} exige quantidade entre {referencia.Opcao.QuantidadeMin} e {referencia.Opcao.QuantidadeMax}.");
                }
            }

            foreach (var secao in secoesAtivas)
            {
                var selecoesDaSecao = secao.Opcoes
                    .Where(o => o.Ativa)
                    .Select(o => new
                    {
                        Opcao = o,
                        Quantidade = opcoesNormalizadas.FirstOrDefault(s => s.ProdutoOpcaoId == o.Id)?.Quantidade ?? 0
                    })
                    .Where(x => x.Quantidade > 0)
                    .ToList();

                var quantidadeSelecoes = selecoesDaSecao.Count;

                if (quantidadeSelecoes < secao.MinSelecoes)
                {
                    return (null, $"A seção {secao.Nome} exige no mínimo {secao.MinSelecoes} opção(ões).");
                }

                if (secao.MaxSelecoes > 0 && quantidadeSelecoes > secao.MaxSelecoes)
                {
                    return (null, $"A seção {secao.Nome} permite no máximo {secao.MaxSelecoes} opção(ões).");
                }
            }

            var snapshots = new List<ItemDePedidoOpcaoSnapshot>();
            var subtotalOpcoes = 0m;

            foreach (var opcaoSelecionada in opcoesNormalizadas)
            {
                var referencia = opcoesDisponiveis[opcaoSelecionada.ProdutoOpcaoId];
                var quantidadeInclusa = Math.Min(opcaoSelecionada.Quantidade, Math.Max(0, referencia.Opcao.Inclusos ?? 0));
                var quantidadeCobradaExtra = Math.Max(0, opcaoSelecionada.Quantidade - quantidadeInclusa);
                var subtotalDelta = referencia.Opcao.PrecoDelta * quantidadeCobradaExtra * quantidade;
                subtotalOpcoes += subtotalDelta;

                snapshots.Add(new ItemDePedidoOpcaoSnapshot
                {
                    ProdutoOpcaoId = referencia.Opcao.Id,
                    NomeSecaoSnapshot = referencia.Secao.Nome,
                    NomeOpcaoSnapshot = referencia.Opcao.Nome,
                    Quantidade = opcaoSelecionada.Quantidade,
                    QuantidadeInclusa = quantidadeInclusa,
                    QuantidadeCobradaExtra = quantidadeCobradaExtra,
                    PrecoUnitarioDeltaSnapshot = referencia.Opcao.PrecoDelta,
                    SubtotalDeltaSnapshot = subtotalDelta
                });
            }

            return (new ItemDePedido
            {
                ProdutoId = produto.Id,
                NomeProdutoSnapshot = produto.Nome,
                PrecoUnitarioSnapshot = produto.Preco,
                Quantidade = quantidade,
                SubtotalItem = (produto.Preco * quantidade) + subtotalOpcoes,
                OpcoesSelecionadas = snapshots
            }, null);
        }

        [HttpGet("api/pedidos/{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetPedido(int id)
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            var pedido = await _context.Pedidos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(p => p.Itens)
                    .ThenInclude(i => i.OpcoesSelecionadas)
                .Include(p => p.HistoricoStatus)
                .FirstOrDefaultAsync(p => p.Id == id && p.ClienteUsuarioId == clienteId.Value);

            if (pedido is null)
            {
                return NotFound();
            }

            return Ok(await MapearPedidoParaDtoAsync(pedido));
        }

        [HttpGet("api/pedidos/meus")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetMeusPedidos()
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            var pedidos = await _context.Pedidos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.ClienteUsuarioId == clienteId.Value)
                .OrderByDescending(p => p.CreatedAtUtc)
                .Select(p => new PedidoResumoDto
                {
                    Id = p.Id,
                    EmpresaId = p.EmpresaId,
                    EmpresaNome = _context.Empresas
                        .Where(e => e.Id == p.EmpresaId)
                        .Select(e => e.Nome)
                        .FirstOrDefault() ?? string.Empty,
                    ClienteUsuarioId = p.ClienteUsuarioId,
                    ContaMesaId = p.ContaMesaId,
                    NomeCliente = p.NomeClienteSnapshot,
                    Status = p.Status,
                    Takeaway = p.Takeaway,
                    NumeroMesa = p.NumeroMesa,
                    TipoAtendimento = p.TipoAtendimento,
                    Total = p.Total,
                    CreatedAtUtc = p.CreatedAtUtc,
                    QuantidadeItens = p.Itens.Sum(i => i.Quantidade)
                })
                .ToListAsync();

            return Ok(pedidos);
        }

        [HttpGet("api/pedidos/conta-aberta")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetContaAbertaAtual()
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            var conta = await ObterContaMesaAbertaDoClienteAsync(clienteId.Value);
            if (conta is null)
            {
                return NotFound();
            }

            var empresaNome = await _context.Empresas
                .IgnoreQueryFilters()
                .Where(e => e.Id == conta.EmpresaId)
                .Select(e => e.Nome)
                .FirstOrDefaultAsync() ?? string.Empty;

            var totalConta = await _context.Pedidos
                .IgnoreQueryFilters()
                .Where(p => p.ContaMesaId == conta.Id && p.Status != StatusPedido.Cancelado)
                .SumAsync(p => p.Total);

            return Ok(new ContaAbertaResumoDto
            {
                ContaMesaId = conta.Id,
                EmpresaId = conta.EmpresaId,
                EmpresaNome = empresaNome,
                NumeroMesa = conta.NumeroMesa,
                CreatedAtUtc = conta.CreatedAtUtc,
                TotalConta = totalConta
            });
        }

        [HttpGet("api/pedidos/conta-aberta/detalhe")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetDetalheContaAbertaAtual()
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            var conta = await ObterContaMesaAbertaDoClienteAsync(clienteId.Value);
            if (conta is null)
            {
                return NotFound();
            }

            var empresaNome = await _context.Empresas
                .IgnoreQueryFilters()
                .Where(e => e.Id == conta.EmpresaId)
                .Select(e => e.Nome)
                .FirstOrDefaultAsync() ?? string.Empty;

            var pedidos = await _context.Pedidos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.ContaMesaId == conta.Id && p.Status != StatusPedido.Cancelado)
                .Include(p => p.Itens)
                    .ThenInclude(i => i.OpcoesSelecionadas)
                .OrderBy(p => p.CreatedAtUtc)
                .ToListAsync();

            var detalhe = new ContaAbertaDetalheDto
            {
                ContaMesaId = conta.Id,
                EmpresaId = conta.EmpresaId,
                EmpresaNome = empresaNome,
                NumeroMesa = conta.NumeroMesa,
                Status = conta.Status,
                CreatedAtUtc = conta.CreatedAtUtc,
                UpdatedAtUtc = conta.UpdatedAtUtc,
                TotalConta = pedidos.Where(p => p.Status != StatusPedido.Cancelado).Sum(p => p.Total),
                Pedidos = pedidos.Select(p => new ContaPedidoDto
                {
                    PedidoId = p.Id,
                    Status = p.Status,
                    TipoAtendimento = p.TipoAtendimento,
                    CreatedAtUtc = p.CreatedAtUtc,
                    Total = p.Total,
                    Itens = p.Itens.Select(i => new ContaPedidoItemDto
                    {
                        NomeProduto = i.NomeProdutoSnapshot,
                        Quantidade = i.Quantidade,
                        PrecoUnitario = i.PrecoUnitarioSnapshot,
                        Subtotal = i.SubtotalItem,
                        Opcoes = i.OpcoesSelecionadas
                            .OrderBy(o => o.NomeSecaoSnapshot)
                            .ThenBy(o => o.NomeOpcaoSnapshot)
                            .Select(o => new ItemDePedidoOpcaoDto
                            {
                                NomeSecao = o.NomeSecaoSnapshot,
                                NomeOpcao = o.NomeOpcaoSnapshot,
                                Quantidade = o.Quantidade,
                                QuantidadeInclusa = o.QuantidadeInclusa,
                                QuantidadeCobradaExtra = o.QuantidadeCobradaExtra,
                                PrecoUnitarioDelta = o.PrecoUnitarioDeltaSnapshot,
                                SubtotalDelta = o.SubtotalDeltaSnapshot
                            }).ToList()
                    }).ToList()
                }).ToList()
            };

            return Ok(detalhe);
        }

        [HttpGet("api/pedidos/checkout/googlepay/config")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetGooglePayConfigCheckout([FromQuery] int empresaId, [FromQuery] decimal total)
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            if (empresaId <= 0)
            {
                return BadRequest(new { message = "Empresa inválida." });
            }

            if (total <= 0)
            {
                return BadRequest(new { message = "Total inválido para o pagamento." });
            }

            var empresaExiste = await _context.Empresas
                .AsNoTracking()
                .AnyAsync(e => e.Id == empresaId);

            if (!empresaExiste)
            {
                return NotFound(new { message = "Empresa não encontrada." });
            }

            var config = _googlePayService.ObterConfiguracaoCliente(total);
            return Ok(new GooglePayConfigResponse
            {
                Environment = config.Environment,
                MerchantName = config.MerchantName,
                MerchantId = config.MerchantId,
                Gateway = config.Gateway,
                GatewayMerchantId = config.GatewayMerchantId,
                CountryCode = config.CountryCode,
                CurrencyCode = config.CurrencyCode,
                ButtonColor = config.ButtonColor,
                TotalPrice = config.TotalPrice
            });
        }

        [HttpPost("api/pedidos/checkout/googlepay/processar")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ProcessarGooglePayCheckout([FromBody] ProcessarPagamentoGooglePayRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new GooglePayProcessResponse
                {
                    Sucesso = false,
                    Mensagem = "O token do Google Pay não foi recebido."
                });
            }

            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            if (!request.EmpresaId.HasValue || request.EmpresaId.Value <= 0 || !request.Total.HasValue || request.Total.Value <= 0)
            {
                return BadRequest(new GooglePayProcessResponse
                {
                    Sucesso = false,
                    Mensagem = "Dados inválidos para processar o pagamento do pedido."
                });
            }

            var empresaExiste = await _context.Empresas
                .AsNoTracking()
                .AnyAsync(e => e.Id == request.EmpresaId.Value);

            if (!empresaExiste)
            {
                return NotFound(new GooglePayProcessResponse
                {
                    Sucesso = false,
                    Mensagem = "Empresa não encontrada."
                });
            }

            var resultado = await _googlePayService.ProcessarPagamentoCheckoutTesteAsync(
                request.EmpresaId.Value,
                request.Total.Value,
                request.Token);

            var resposta = new GooglePayProcessResponse
            {
                Sucesso = resultado.Sucesso,
                Mensagem = resultado.Mensagem,
                Referencia = resultado.Referencia
            };

            return resultado.Sucesso ? Ok(resposta) : BadRequest(resposta);
        }

        [HttpGet("api/pedidos/conta-aberta/googlepay/config")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetGooglePayConfigContaAbertaAtual()
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            var conta = await ObterContaMesaAbertaDoClienteAsync(clienteId.Value);
            if (conta is null)
            {
                return NotFound(new { message = "Você não possui conta aberta." });
            }

            var totalConta = await _context.Pedidos
                .IgnoreQueryFilters()
                .Where(p => p.ContaMesaId == conta.Id && p.Status != StatusPedido.Cancelado)
                .SumAsync(p => p.Total);

            if (totalConta <= 0)
            {
                return BadRequest(new { message = "Não há itens pendentes para pagamento nesta conta." });
            }

            var config = _googlePayService.ObterConfiguracaoCliente(totalConta);
            return Ok(new GooglePayConfigResponse
            {
                Environment = config.Environment,
                MerchantName = config.MerchantName,
                MerchantId = config.MerchantId,
                Gateway = config.Gateway,
                GatewayMerchantId = config.GatewayMerchantId,
                CountryCode = config.CountryCode,
                CurrencyCode = config.CurrencyCode,
                ButtonColor = config.ButtonColor,
                TotalPrice = config.TotalPrice
            });
        }

        [HttpPost("api/pedidos/conta-aberta/googlepay/processar")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ProcessarGooglePayContaAbertaAtual([FromBody] ProcessarPagamentoGooglePayRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new GooglePayProcessResponse
                {
                    Sucesso = false,
                    Mensagem = "O token do Google Pay não foi recebido."
                });
            }

            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            var conta = await ObterContaMesaAbertaDoClienteAsync(clienteId.Value);
            if (conta is null)
            {
                return NotFound(new GooglePayProcessResponse
                {
                    Sucesso = false,
                    Mensagem = "Você não possui conta aberta."
                });
            }

            var totalConta = await _context.Pedidos
                .IgnoreQueryFilters()
                .Where(p => p.ContaMesaId == conta.Id && p.Status != StatusPedido.Cancelado)
                .SumAsync(p => p.Total);

            var resultado = await _googlePayService.ProcessarPagamentoTesteAsync(conta.Id, totalConta, request.Token);
            var resposta = new GooglePayProcessResponse
            {
                Sucesso = resultado.Sucesso,
                Mensagem = resultado.Mensagem,
                Referencia = resultado.Referencia
            };

            if (!resultado.Sucesso)
            {
                return BadRequest(resposta);
            }

            await _servicoTempoRealPedidos.NotificarContaAbertaAtualizadaAsync(conta.ClienteUsuarioId);
            return Ok(resposta);
        }

        [HttpPost("api/pedidos/conta-aberta/pagar")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> PagarContaAbertaAtual()
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            var conta = await ObterContaMesaAbertaDoClienteAsync(clienteId.Value);
            if (conta is null)
            {
                return NotFound(new { message = "Você não possui conta aberta visível no momento." });
            }

            var agoraUtc = DateTime.UtcNow;
            var totalConta = await _context.Pedidos
                .IgnoreQueryFilters()
                .Where(p => p.ContaMesaId == conta.Id && p.Status != StatusPedido.Cancelado)
                .SumAsync(p => p.Total);

            conta.Status = StatusContaMesa.Fechada;
            conta.UpdatedAtUtc = agoraUtc;
            conta.FechadaAtUtc = agoraUtc;

            if (totalConta > 0)
            {
                _context.Transacoes.Add(new Transacao
                {
                    EmpresaId = conta.EmpresaId,
                    Categoria = CategoriaDeTransacao.Receita,
                    Descricao = $"Pagamento da conta #{conta.Id} (Mesa {conta.NumeroMesa})",
                    Valor = totalConta,
                    DataDeCadastro = agoraUtc,
                    DataRetroativa = null
                });
            }

            await _context.SaveChangesAsync();
            await _servicoTempoRealPedidos.NotificarContaAbertaAtualizadaAsync(conta.ClienteUsuarioId);

            return Ok(new { message = "Conta finalizada com sucesso." });
        }

        [HttpPost("api/contas-mesa/{contaMesaId:int}/pagar")]
        [Authorize]
        public async Task<IActionResult> PagarContaMesaPelaEmpresa(int contaMesaId)
        {
            if (!_provedorDeTenant.TemTenant)
            {
                return Forbid();
            }

            var conta = await _context.ContasMesa
                .FirstOrDefaultAsync(c => c.Id == contaMesaId);

            if (conta is null)
            {
                return NotFound();
            }

            if (conta.EmpresaId != _provedorDeTenant.EmpresaId)
            {
                return Forbid();
            }

            if (conta.Status == StatusContaMesa.Fechada)
            {
                return BadRequest(new { message = "Conta já está fechada." });
            }

            var agoraUtc = DateTime.UtcNow;
            var totalConta = await _context.Pedidos
                .Where(p => p.ContaMesaId == conta.Id && p.Status != StatusPedido.Cancelado)
                .SumAsync(p => p.Total);

            conta.Status = StatusContaMesa.Fechada;
            conta.UpdatedAtUtc = agoraUtc;
            conta.FechadaAtUtc = agoraUtc;

            if (totalConta > 0)
            {
                _context.Transacoes.Add(new Transacao
                {
                    EmpresaId = conta.EmpresaId,
                    Categoria = CategoriaDeTransacao.Receita,
                    Descricao = $"Pagamento da conta #{conta.Id} (Mesa {conta.NumeroMesa})",
                    Valor = totalConta,
                    DataDeCadastro = agoraUtc,
                    DataRetroativa = null
                });
            }

            await _context.SaveChangesAsync();
            await _servicoTempoRealPedidos.NotificarContaAbertaAtualizadaAsync(conta.ClienteUsuarioId);

            return Ok(new { message = "Conta finalizada com sucesso." });
        }

        [HttpGet("api/empresas/{empresaId:int}/pedidos")]
        [Authorize]
        public async Task<IActionResult> GetPedidosDaEmpresa(
            int empresaId,
            [FromQuery] string? status,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            if (!_provedorDeTenant.TemTenant)
            {
                return Forbid();
            }

            if (_provedorDeTenant.EmpresaId != empresaId)
            {
                return Forbid();
            }

            IQueryable<Pedido> query = _context.Pedidos
                .AsNoTracking()
                .Where(p => p.EmpresaId == empresaId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<StatusPedido>(status, true, out var statusPedido))
                {
                    return BadRequest(new { message = "Status inválido." });
                }

                query = query.Where(p => p.Status == statusPedido);
            }

            if (from.HasValue)
            {
                query = query.Where(p => p.CreatedAtUtc >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(p => p.CreatedAtUtc <= to.Value);
            }

            var pedidos = await query
                .OrderByDescending(p => p.CreatedAtUtc)
                .Select(p => new PedidoResumoDto
                {
                    Id = p.Id,
                    EmpresaId = p.EmpresaId,
                    EmpresaNome = _context.Empresas
                        .Where(e => e.Id == p.EmpresaId)
                        .Select(e => e.Nome)
                        .FirstOrDefault() ?? string.Empty,
                    ClienteUsuarioId = p.ClienteUsuarioId,
                    ContaMesaId = p.ContaMesaId,
                    NomeCliente = p.NomeClienteSnapshot,
                    Status = p.Status,
                    Takeaway = p.Takeaway,
                    NumeroMesa = p.NumeroMesa,
                    TipoAtendimento = p.TipoAtendimento,
                    Total = p.Total,
                    CreatedAtUtc = p.CreatedAtUtc,
                    QuantidadeItens = p.Itens.Sum(i => i.Quantidade)
                })
                .ToListAsync();

            return Ok(pedidos);
        }

        [HttpPost("api/pedidos/{id:int}/confirmar-entrega")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ConfirmarEntrega(int id)
        {
            var clienteId = ObterClienteId();
            if (clienteId is null)
            {
                return Unauthorized();
            }

            var pedido = await _servicoWorkflowPedido.ObterPedidoComHistoricoAsync(id, ignorarFiltros: true);
            if (pedido is null || pedido.ClienteUsuarioId != clienteId.Value)
            {
                return NotFound();
            }

            if (pedido.Status != StatusPedido.Enviado)
            {
                return BadRequest(new { message = "Somente pedidos enviados podem ser confirmados." });
            }

            if (pedido.TipoAtendimento == TipoAtendimentoPedido.ComerAqui)
            {
                return BadRequest(new { message = "Pedidos para comer aqui não exigem confirmação de entrega do cliente." });
            }

            var atualizou = await _servicoWorkflowPedido.AtualizarStatusAsync(pedido, StatusPedido.Concluido, "Cliente");
            if (!atualizou)
            {
                return BadRequest(new { message = "Transição de status inválida." });
            }

            return Ok(await MapearPedidoParaDtoAsync(pedido));
        }

        [HttpPost("api/pedidos/{id:int}/aceitar")]
        [Authorize]
        public async Task<IActionResult> AceitarPedido(int id)
        {
            if (!_provedorDeTenant.TemTenant)
            {
                return Forbid();
            }

            var pedido = await _servicoWorkflowPedido.ObterPedidoComHistoricoAsync(id);
            if (pedido is null)
            {
                return NotFound();
            }

            if (pedido.EmpresaId != _provedorDeTenant.EmpresaId)
            {
                return Forbid();
            }

            var atualizou = await _servicoWorkflowPedido.AtualizarStatusAsync(pedido, StatusPedido.Confirmado, "Empresa");
            if (!atualizou)
            {
                return BadRequest(new { message = "Transição de status inválida." });
            }

            return Ok(await MapearPedidoParaDtoAsync(pedido));
        }

        [HttpPost("api/pedidos/{id:int}/recusar")]
        [Authorize]
        public async Task<IActionResult> RecusarPedido(int id)
        {
            if (!_provedorDeTenant.TemTenant)
            {
                return Forbid();
            }

            var pedido = await _servicoWorkflowPedido.ObterPedidoComHistoricoAsync(id);
            if (pedido is null)
            {
                return NotFound();
            }

            if (pedido.EmpresaId != _provedorDeTenant.EmpresaId)
            {
                return Forbid();
            }

            var atualizou = await _servicoWorkflowPedido.AtualizarStatusAsync(pedido, StatusPedido.Cancelado, "Empresa");
            if (!atualizou)
            {
                return BadRequest(new { message = "Transição de status inválida." });
            }

            return Ok(await MapearPedidoParaDtoAsync(pedido));
        }

        [HttpPost("api/pedidos/{id:int}/avancar-status")]
        [Authorize]
        public async Task<IActionResult> AvancarStatusPedido(int id)
        {
            if (!_provedorDeTenant.TemTenant)
            {
                return Forbid();
            }

            var pedido = await _servicoWorkflowPedido.ObterPedidoComHistoricoAsync(id);
            if (pedido is null)
            {
                return NotFound();
            }

            if (pedido.EmpresaId != _provedorDeTenant.EmpresaId)
            {
                return Forbid();
            }

            var proximoStatus = ServicoWorkflowPedido.ObterProximoStatusEmpresa(pedido);
            if (!proximoStatus.HasValue)
            {
                return BadRequest(new { message = "Pedido não pode ser avançado pelo fluxo da empresa no status atual." });
            }

            var atualizou = await _servicoWorkflowPedido.AtualizarStatusAsync(pedido, proximoStatus.Value, "Empresa");
            if (!atualizou)
            {
                return BadRequest(new { message = "Transição de status inválida." });
            }

            return Ok(await MapearPedidoParaDtoAsync(pedido));
        }

        private int? ObterClienteId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var clienteId))
            {
                return null;
            }

            return clienteId;
        }

        private Task<ContaMesa?> ObterContaMesaAbertaDoClienteAsync(int clienteId)
        {
            return _context.ContasMesa
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(c => c.Pedidos)
                .Where(c => c.Status == StatusContaMesa.Aberta
                    && c.Pedidos.Any(p => p.Status != StatusPedido.Cancelado && p.Status != StatusPedido.Criado)
                    && (c.ClienteUsuarioId == clienteId
                        || c.Pedidos.Any(p => p.ClienteUsuarioId == clienteId && p.Status != StatusPedido.Cancelado)))
                .OrderByDescending(c => c.UpdatedAtUtc)
                .ThenByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync();
        }

        private async Task<PedidoDto> MapearPedidoParaDtoAsync(Pedido pedido)
        {
            var empresaNome = await _context.Empresas
                .Where(e => e.Id == pedido.EmpresaId)
                .Select(e => e.Nome)
                .FirstOrDefaultAsync() ?? string.Empty;

            return new PedidoDto
            {
                Id = pedido.Id,
                EmpresaId = pedido.EmpresaId,
                EmpresaNome = empresaNome,
                ClienteUsuarioId = pedido.ClienteUsuarioId,
                ContaMesaId = pedido.ContaMesaId,
                NomeCliente = pedido.NomeClienteSnapshot,
                EmailCliente = pedido.EmailClienteSnapshot,
                Status = pedido.Status,
                Takeaway = pedido.Takeaway,
                NumeroMesa = pedido.NumeroMesa,
                TipoAtendimento = pedido.TipoAtendimento,
                Origem = pedido.Origem,
                Moeda = pedido.Moeda,
                Subtotal = pedido.Subtotal,
                Desconto = pedido.Desconto,
                Frete = pedido.Frete,
                Total = pedido.Total,
                EnderecoEntrega = pedido.EnderecoEntrega,
                Observacoes = pedido.Observacoes,
                CreatedAtUtc = pedido.CreatedAtUtc,
                UpdatedAtUtc = pedido.UpdatedAtUtc,
                Itens = pedido.Itens.Select(i => new ItemDePedidoDto
                {
                    ProdutoId = i.ProdutoId,
                    NomeProduto = i.NomeProdutoSnapshot,
                    PrecoUnitario = i.PrecoUnitarioSnapshot,
                    Quantidade = i.Quantidade,
                    Subtotal = i.SubtotalItem,
                    Opcoes = i.OpcoesSelecionadas
                        .OrderBy(o => o.NomeSecaoSnapshot)
                        .ThenBy(o => o.NomeOpcaoSnapshot)
                        .Select(o => new ItemDePedidoOpcaoDto
                        {
                            NomeSecao = o.NomeSecaoSnapshot,
                            NomeOpcao = o.NomeOpcaoSnapshot,
                            Quantidade = o.Quantidade,
                            QuantidadeInclusa = o.QuantidadeInclusa,
                            QuantidadeCobradaExtra = o.QuantidadeCobradaExtra,
                            PrecoUnitarioDelta = o.PrecoUnitarioDeltaSnapshot,
                            SubtotalDelta = o.SubtotalDeltaSnapshot
                        }).ToList()
                }).ToList(),
                HistoricoStatus = pedido.HistoricoStatus
                    .OrderBy(h => h.DataStatusUtc)
                    .Select(h => new HistoricoStatusPedidoDto
                    {
                        Status = h.Status,
                        DataStatusUtc = h.DataStatusUtc,
                        RegistradoPor = h.RegistradoPor
                    })
                    .ToList()
            };
        }
    }
}