using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public interface IImagemPublicaService
    {
        string? ConstruirUrlProduto(Produto? produto);
        string ConstruirUrlProduto(int empresaId, int produtoId, string? nomeProduto);
        Task<ImagemPublicaArquivo?> ObterArquivoProdutoAsync(int empresaId, int produtoId, CancellationToken cancellationToken = default);
    }

    public sealed record ImagemPublicaArquivo(
        string CaminhoFisico,
        string ContentType,
        string UrlCanonica,
        DateTimeOffset UltimaModificacaoUtc);

    public sealed class ImagemPublicaService : IImagemPublicaService
    {
        private readonly AppDbContext _context;
        private readonly IProdutoImagemStorage _produtoImagemStorage;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

        public ImagemPublicaService(
            AppDbContext context,
            IProdutoImagemStorage produtoImagemStorage,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _context = context;
            _produtoImagemStorage = produtoImagemStorage;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public string? ConstruirUrlProduto(Produto? produto)
        {
            if (produto is null
                || produto.EmpresaId <= 0
                || produto.Id <= 0
                || string.IsNullOrWhiteSpace(produto.ImagemUrl))
            {
                return null;
            }

            return ConstruirUrlProduto(produto.EmpresaId, produto.Id, produto.Nome);
        }

        public string ConstruirUrlProduto(int empresaId, int produtoId, string? nomeProduto)
        {
            var baseSlug = string.IsNullOrWhiteSpace(nomeProduto)
                ? $"produto-{produtoId}"
                : nomeProduto;

            var slug = _produtoImagemStorage.GerarSlugPublico(baseSlug, "produto");
            var caminhoRelativo = $"/img/produtos/{empresaId}/{produtoId}/{slug}";
            var origemPublica = ObterOrigemPublica();

            return string.IsNullOrWhiteSpace(origemPublica)
                ? caminhoRelativo
                : $"{origemPublica}{caminhoRelativo}";
        }

        private string? ObterOrigemPublica()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request is not null && request.Host.HasValue)
            {
                return $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
            }

            var appUrl = _configuration["AppUrl"];
            return string.IsNullOrWhiteSpace(appUrl)
                ? null
                : appUrl.TrimEnd('/');
        }

        public async Task<ImagemPublicaArquivo?> ObterArquivoProdutoAsync(int empresaId, int produtoId, CancellationToken cancellationToken = default)
        {
            if (empresaId <= 0 || produtoId <= 0)
            {
                return null;
            }

            var produto = await _context.Produtos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.EmpresaId == empresaId && p.Id == produtoId, cancellationToken);

            if (produto is null || string.IsNullOrWhiteSpace(produto.ImagemUrl))
            {
                return null;
            }

            var caminhoFisico = _produtoImagemStorage.TentarResolverCaminhoFisico(produto.ImagemUrl);
            if (string.IsNullOrWhiteSpace(caminhoFisico) || !File.Exists(caminhoFisico))
            {
                return null;
            }

            if (!_contentTypeProvider.TryGetContentType(caminhoFisico, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            var fileInfo = new FileInfo(caminhoFisico);

            return new ImagemPublicaArquivo(
                caminhoFisico,
                contentType,
                ConstruirUrlProduto(produto.EmpresaId, produto.Id, produto.Nome),
                fileInfo.LastWriteTimeUtc);
        }
    }
}
