using Microsoft.Extensions.Options;

namespace Restaurapp.BlazorServer.Services
{
    public interface IProdutoImagemStorage
    {
        long MaxFileSizeBytes { get; }
        Task<string> SalvarImagemProdutoAsync(int empresaId, int produtoId, string nomeProduto, string nomeArquivoOriginal, Stream conteudo, string? imagemUrlAtual, CancellationToken cancellationToken = default);
        Task ExcluirImagemProdutoAsync(string? imagemUrl, CancellationToken cancellationToken = default);
    }

    public sealed class ProdutoImagemStorage : IProdutoImagemStorage
    {
        private static readonly HashSet<string> ExtensoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private readonly IWebHostEnvironment _environment;
        private readonly UploadsOptions _options;
        private readonly ILogger<ProdutoImagemStorage> _logger;

        public ProdutoImagemStorage(
            IWebHostEnvironment environment,
            IOptions<UploadsOptions> options,
            ILogger<ProdutoImagemStorage> logger)
        {
            _environment = environment;
            _options = options.Value;
            _logger = logger;
        }

        public long MaxFileSizeBytes => _options.MaxFileSizeBytes;

        public async Task<string> SalvarImagemProdutoAsync(
            int empresaId,
            int produtoId,
            string nomeProduto,
            string nomeArquivoOriginal,
            Stream conteudo,
            string? imagemUrlAtual,
            CancellationToken cancellationToken = default)
        {
            if (empresaId <= 0)
            {
                throw new InvalidOperationException("Empresa inválida para armazenamento da imagem.");
            }

            var extensao = Path.GetExtension(nomeArquivoOriginal);
            if (string.IsNullOrWhiteSpace(extensao) || !ExtensoesPermitidas.Contains(extensao))
            {
                throw new InvalidOperationException("Formato de imagem inválido. Use JPG, PNG ou WEBP.");
            }

            var pastaEmpresa = Path.Combine(ObterRaizUploads(), $"restaurante-{empresaId}");
            Directory.CreateDirectory(pastaEmpresa);

            var slug = GerarSlug(string.IsNullOrWhiteSpace(nomeProduto)
                ? Path.GetFileNameWithoutExtension(nomeArquivoOriginal)
                : nomeProduto);

            var nomeArquivo = $"produto-{produtoId}-{slug}{extensao.ToLowerInvariant()}";
            var caminhoDestino = Path.Combine(pastaEmpresa, nomeArquivo);
            var urlRelativa = $"/uploads/restaurante-{empresaId}/{nomeArquivo}";

            await using (var destino = File.Create(caminhoDestino))
            {
                await conteudo.CopyToAsync(destino, cancellationToken);
            }

            if (!string.Equals(imagemUrlAtual, urlRelativa, StringComparison.OrdinalIgnoreCase))
            {
                await ExcluirImagemProdutoAsync(imagemUrlAtual, cancellationToken);
            }

            return urlRelativa;
        }

        public Task ExcluirImagemProdutoAsync(string? imagemUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imagemUrl))
            {
                return Task.CompletedTask;
            }

            var caminhoArquivo = ResolverCaminhoFisico(imagemUrl);
            if (caminhoArquivo is null || !File.Exists(caminhoArquivo))
            {
                return Task.CompletedTask;
            }

            try
            {
                File.Delete(caminhoArquivo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao excluir arquivo de imagem {ImagemUrl}.", imagemUrl);
            }

            return Task.CompletedTask;
        }

        public string ObterRaizUploads()
        {
            var rootPath = _options.RootPath;

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                rootPath = "uploads";
            }

            var caminho = Path.IsPathRooted(rootPath)
                ? rootPath
                : Path.Combine(_environment.ContentRootPath, rootPath);

            return Path.GetFullPath(caminho);
        }

        private string? ResolverCaminhoFisico(string imagemUrl)
        {
            var caminhoSemQuery = imagemUrl.Split('?', 2)[0];
            if (!caminhoSemQuery.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relativo = caminhoSemQuery["/uploads/".Length..]
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var raizUploads = ObterRaizUploads();
            var caminhoCompleto = Path.GetFullPath(Path.Combine(raizUploads, relativo));

            if (!caminhoCompleto.StartsWith(raizUploads, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return caminhoCompleto;
        }

        private static string GerarSlug(string valor)
        {
            var caracteres = valor
                .Trim()
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();

            var slug = new string(caracteres);

            while (slug.Contains("--", StringComparison.Ordinal))
            {
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            }

            slug = slug.Trim('-');

            return string.IsNullOrWhiteSpace(slug) ? "produto" : slug;
        }
    }
}