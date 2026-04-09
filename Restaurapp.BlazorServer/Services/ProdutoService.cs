using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public class ProdutoService
    {
        public sealed record ProdutoOpcaoInput(
            string Nome,
            string? Descricao,
            decimal PrecoDelta,
            int QuantidadeMin,
            int QuantidadeMax,
            int? Inclusos,
            bool Ativa = true);

        public sealed record ProdutoOpcaoSecaoInput(
            string Nome,
            int MinSelecoes,
            int MaxSelecoes,
            bool PermitirQuantidade,
            bool Ativa,
            List<ProdutoOpcaoInput> Opcoes);

        private readonly AppDbContext _context;
        private readonly IProdutoImagemStorage _produtoImagemStorage;

        public ProdutoService(AppDbContext context, IProdutoImagemStorage produtoImagemStorage)
        {
            _context = context;
            _produtoImagemStorage = produtoImagemStorage;
        }

        public async Task<List<Produto>> ObterProdutosAsync()
        {
            return await _context.Produtos
                .Include(p => p.OpcoesSecoes)
                    .ThenInclude(s => s.Opcoes)
                .OrderBy(p => p.Secao)
                .ThenBy(p => p.Nome)
                .ToListAsync();
        }

        public async Task<List<string>> ObterSecoesAsync()
        {
            return await _context.Produtos
                .Select(p => p.Secao)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        public async Task<Produto?> ObterProdutoPorIdAsync(int id)
        {
            return await _context.Produtos
                .Include(p => p.OpcoesSecoes)
                    .ThenInclude(s => s.Opcoes)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Produto> CadastrarProdutoAsync(
            string secao,
            string nome,
            string? descricao,
            decimal valor,
            IEnumerable<ProdutoOpcaoSecaoInput>? opcoesSecoes = null)
        {
            var produto = new Produto
            {
                EmpresaId = _context.EmpresaId,
                Secao = secao,
                Nome = nome,
                Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
                Preco = valor,
                OpcoesSecoes = ConstruirSecoesDeOpcao(opcoesSecoes)
            };

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();
            return produto;
        }

        public async Task<bool> AtualizarProdutoAsync(
            int id,
            string secao,
            string nome,
            string? descricao,
            decimal valor,
            IEnumerable<ProdutoOpcaoSecaoInput>? opcoesSecoes = null)
        {
            var produto = await _context.Produtos
                .Include(p => p.OpcoesSecoes)
                    .ThenInclude(s => s.Opcoes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto is null)
            {
                return false;
            }

            produto.Secao = secao;
            produto.Nome = nome;
            produto.Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
            produto.Preco = valor;

            var secoesExistentes = produto.OpcoesSecoes.ToList();
            var opcoesExistentes = secoesExistentes.SelectMany(s => s.Opcoes).ToList();

            if (opcoesExistentes.Count > 0)
            {
                _context.ProdutoOpcoes.RemoveRange(opcoesExistentes);
            }

            if (secoesExistentes.Count > 0)
            {
                _context.ProdutoOpcoesSecoes.RemoveRange(secoesExistentes);
            }

            produto.OpcoesSecoes = ConstruirSecoesDeOpcao(opcoesSecoes);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Produto?> AtualizarImagemProdutoAsync(
            int id,
            string nomeArquivoOriginal,
            Stream conteudo,
            CancellationToken cancellationToken = default)
        {
            var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (produto is null)
            {
                return null;
            }

            var novaImagemUrl = await _produtoImagemStorage.SalvarImagemProdutoAsync(
                produto.EmpresaId,
                produto.Id,
                produto.Nome,
                nomeArquivoOriginal,
                conteudo,
                produto.ImagemUrl,
                cancellationToken);

            produto.ImagemUrl = novaImagemUrl;
            await _context.SaveChangesAsync(cancellationToken);
            return produto;
        }

        public async Task<bool> DeletarProdutoAsync(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);

            if (produto is null)
            {
                return false;
            }

            var imagemUrl = produto.ImagemUrl;
            _context.Produtos.Remove(produto);
            await _context.SaveChangesAsync();
            await _produtoImagemStorage.ExcluirImagemProdutoAsync(imagemUrl);
            return true;
        }

        private static List<ProdutoOpcaoSecao> ConstruirSecoesDeOpcao(IEnumerable<ProdutoOpcaoSecaoInput>? opcoesSecoes)
        {
            if (opcoesSecoes is null)
            {
                return new List<ProdutoOpcaoSecao>();
            }

            var secoes = new List<ProdutoOpcaoSecao>();
            var indiceSecao = 0;

            foreach (var secao in opcoesSecoes)
            {
                if (string.IsNullOrWhiteSpace(secao.Nome))
                {
                    continue;
                }

                var nomeSecao = secao.Nome.Trim();
                var opcoes = new List<ProdutoOpcao>();
                var indiceOpcao = 0;

                foreach (var opcao in secao.Opcoes)
                {
                    if (string.IsNullOrWhiteSpace(opcao.Nome))
                    {
                        continue;
                    }

                    var quantidadeMin = Math.Max(0, opcao.QuantidadeMin);
                    var quantidadeMax = opcao.QuantidadeMax <= 0
                        ? Math.Max(1, quantidadeMin == 0 ? 1 : quantidadeMin)
                        : Math.Max(opcao.QuantidadeMax, quantidadeMin == 0 ? 1 : quantidadeMin);
                    var inclusos = Math.Max(0, opcao.Inclusos ?? 0);

                    if (!secao.PermitirQuantidade)
                    {
                        inclusos = Math.Min(inclusos, 1);
                    }

                    inclusos = Math.Min(inclusos, quantidadeMax);

                    opcoes.Add(new ProdutoOpcao
                    {
                        Nome = opcao.Nome.Trim(),
                        Descricao = string.IsNullOrWhiteSpace(opcao.Descricao) ? null : opcao.Descricao.Trim(),
                        PrecoDelta = opcao.PrecoDelta,
                        QuantidadeMin = quantidadeMin,
                        QuantidadeMax = quantidadeMax,
                        Inclusos = inclusos > 0 ? inclusos : null,
                        Ativa = opcao.Ativa,
                        Ordem = indiceOpcao++
                    });
                }

                var minSelecoes = Math.Max(0, secao.MinSelecoes);
                var maxSelecoes = secao.MaxSelecoes < 0 ? 0 : secao.MaxSelecoes;
                if (maxSelecoes > 0 && maxSelecoes < minSelecoes)
                {
                    maxSelecoes = minSelecoes;
                }

                secoes.Add(new ProdutoOpcaoSecao
                {
                    Nome = nomeSecao,
                    MinSelecoes = minSelecoes,
                    MaxSelecoes = maxSelecoes,
                    PermitirQuantidade = secao.PermitirQuantidade,
                    Ativa = secao.Ativa,
                    Ordem = indiceSecao++,
                    Opcoes = opcoes
                });
            }

            return secoes;
        }
    }
}