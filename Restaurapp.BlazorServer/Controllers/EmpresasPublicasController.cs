using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Services;

namespace Restaurapp.BlazorServer.Controllers
{
    [ApiController]
    [Route("api/public/empresas")]
    public class EmpresasPublicasController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IImagemPublicaService _imagemPublicaService;

        public EmpresasPublicasController(AppDbContext context, IImagemPublicaService imagemPublicaService)
        {
            _context = context;
            _imagemPublicaService = imagemPublicaService;
        }

        [HttpGet]
        public async Task<IActionResult> GetEmpresas()
        {
            var empresas = await _context.Empresas
                .OrderBy(e => e.Nome)
                .Select(e => new EmpresaPublicaDto
                {
                    Id = e.Id,
                    Nome = e.Nome,
                    HabilitarContasPosPagas = e.HabilitarContasPosPagas
                })
                .ToListAsync();

            return Ok(empresas);
        }

        [HttpGet("{empresaId:int}")]
        public async Task<IActionResult> GetEmpresa(int empresaId)
        {
            var empresa = await _context.Empresas
                .AsNoTracking()
                .Where(e => e.Id == empresaId)
                .Select(e => new EmpresaCatalogoDto
                {
                    Id = e.Id,
                    Nome = e.Nome,
                    HabilitarContasPosPagas = e.HabilitarContasPosPagas
                })
                .FirstOrDefaultAsync();

            if (empresa is null)
            {
                return NotFound();
            }

            var mapaOrdemSecoes = await _context.SecoesCardapio
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.EmpresaId == empresaId && s.Ativa)
                .OrderBy(s => s.OrdemNoCardapio)
                .ThenBy(s => s.Nome)
                .ToDictionaryAsync(s => s.Nome, s => s.OrdemNoCardapio, StringComparer.OrdinalIgnoreCase);

            empresa.Produtos = await _context.Produtos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.EmpresaId == empresaId && p.Ativo)
                .OrderBy(p => p.OrdemNoCardapio)
                .ThenBy(p => p.Nome)
                .Select(p => new ProdutoCatalogoDto
                {
                    Id = p.Id,
                    Secao = p.Secao,
                    Nome = p.Nome,
                    OrdemNoCardapio = p.OrdemNoCardapio,
                    Preco = p.Preco,
                    ImagemUrl = p.ImagemUrl,
                    OpcoesSecoes = p.OpcoesSecoes
                        .Where(s => s.Ativa)
                        .OrderBy(s => s.Ordem)
                        .Select(s => new ProdutoOpcaoSecaoCatalogoDto
                        {
                            Id = s.Id,
                            Nome = s.Nome,
                            Ordem = s.Ordem,
                            MinSelecoes = s.MinSelecoes,
                            MaxSelecoes = s.MaxSelecoes,
                            PermitirQuantidade = s.PermitirQuantidade,
                            Opcoes = s.Opcoes
                                .Where(o => o.Ativa)
                                .OrderBy(o => o.Ordem)
                                .Select(o => new ProdutoOpcaoCatalogoDto
                                {
                                    Id = o.Id,
                                    Nome = o.Nome,
                                    Descricao = o.Descricao,
                                    PrecoDelta = o.PrecoDelta,
                                    QuantidadeMin = o.QuantidadeMin,
                                    QuantidadeMax = o.QuantidadeMax,
                                    Inclusos = o.Inclusos
                                }).ToList()
                        }).ToList()
                })
                .ToListAsync();

            empresa.Produtos = empresa.Produtos
                .OrderBy(p => mapaOrdemSecoes.TryGetValue(p.Secao, out var ordemSecao) ? ordemSecao : int.MaxValue)
                .ThenBy(p => p.OrdemNoCardapio)
                .ThenBy(p => p.Nome)
                .ToList();

            foreach (var produto in empresa.Produtos)
            {
                if (!string.IsNullOrWhiteSpace(produto.ImagemUrl))
                {
                    produto.ImagemUrl = _imagemPublicaService.ConstruirUrlProduto(empresa.Id, produto.Id, produto.Nome);
                }
            }

            return Ok(empresa);
        }
    }
}
