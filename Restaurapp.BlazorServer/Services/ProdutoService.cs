using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public class ProdutoService
    {
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
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Produto> CadastrarProdutoAsync(string secao, string nome, string? descricao, decimal valor)
        {
            var produto = new Produto
            {
                EmpresaId = _context.EmpresaId,
                Secao = secao,
                Nome = nome,
                Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
                Preco = valor
            };

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();
            return produto;
        }

        public async Task<bool> AtualizarProdutoAsync(int id, string secao, string nome, string? descricao, decimal valor)
        {
            var produto = await _context.Produtos.FindAsync(id);

            if (produto is null)
            {
                return false;
            }

            produto.Secao = secao;
            produto.Nome = nome;
            produto.Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
            produto.Preco = valor;

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
    }
}