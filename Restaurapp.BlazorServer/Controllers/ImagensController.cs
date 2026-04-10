using Microsoft.AspNetCore.Mvc;
using Restaurapp.BlazorServer.Services;

namespace Restaurapp.BlazorServer.Controllers
{
    [ApiController]
    [Route("img")]
    public class ImagensController : ControllerBase
    {
        private readonly IImagemPublicaService _imagemPublicaService;

        public ImagensController(IImagemPublicaService imagemPublicaService)
        {
            _imagemPublicaService = imagemPublicaService;
        }

        [HttpGet("produtos/{empresaId:int}/{produtoId:int}/{slug?}")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> ObterImagemProduto(int empresaId, int produtoId, string? slug, CancellationToken cancellationToken)
        {
            var arquivo = await _imagemPublicaService.ObterArquivoProdutoAsync(empresaId, produtoId, cancellationToken);
            if (arquivo is null)
            {
                return NotFound();
            }

            var urlAtual = HttpContext.Request.PathBase.Add(HttpContext.Request.Path).Value ?? string.Empty;
            var caminhoCanonico = Uri.TryCreate(arquivo.UrlCanonica, UriKind.Absolute, out var uriCanonica)
                ? uriCanonica.PathAndQuery
                : arquivo.UrlCanonica;

            if (!string.Equals(urlAtual, caminhoCanonico, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectPermanent(arquivo.UrlCanonica);
            }

            Response.Headers["Cache-Control"] = "public,max-age=86400";
            Response.Headers["Last-Modified"] = arquivo.UltimaModificacaoUtc.ToUniversalTime().ToString("R");

            return PhysicalFile(arquivo.CaminhoFisico, arquivo.ContentType, enableRangeProcessing: true);
        }
    }
}
