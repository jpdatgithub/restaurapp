using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;

namespace Restaurapp.BlazorServer.Controllers
{
    [ApiController]
    [Route("api/public/empresas")]
    public class EmpresasPublicasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EmpresasPublicasController(AppDbContext context)
        {
            _context = context;
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

            empresa.Produtos = await _context.Produtos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.EmpresaId == empresaId && p.Ativo)
                .OrderBy(p => p.Nome)
                .Select(p => new ProdutoCatalogoDto
                {
                    Id = p.Id,
                    Nome = p.Nome,
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
                                    QuantidadeMax = o.QuantidadeMax
                                }).ToList()
                        }).ToList()
                })
                .ToListAsync();

            return Ok(empresa);
        }
    }
}
