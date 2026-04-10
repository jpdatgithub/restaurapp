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
            await GarantirSecoesCardapioParaProdutosAsync();

            var mapaOrdemSecoes = await ObterMapaOrdemSecoesAsync();
            var produtos = await _context.Produtos
                .Include(p => p.OpcoesSecoes)
                    .ThenInclude(s => s.Opcoes)
                .ToListAsync();

            return produtos
                .OrderBy(p => mapaOrdemSecoes.TryGetValue(NormalizarSecao(p.Secao), out var ordemSecao) ? ordemSecao : int.MaxValue)
                .ThenBy(p => p.OrdemNoCardapio)
                .ThenBy(p => p.Nome)
                .ToList();
        }

        public async Task<List<string>> ObterSecoesAsync()
        {
            await GarantirSecoesCardapioParaProdutosAsync();

            var secoesOrdenadas = await _context.SecoesCardapio
                .OrderBy(s => s.OrdemNoCardapio)
                .ThenBy(s => s.Nome)
                .Select(s => s.Nome)
                .ToListAsync();

            var secoesDosProdutos = await _context.Produtos
                .Select(p => p.Secao)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            foreach (var secao in secoesDosProdutos)
            {
                if (!secoesOrdenadas.Any(s => string.Equals(s, secao, StringComparison.OrdinalIgnoreCase)))
                {
                    secoesOrdenadas.Add(secao);
                }
            }

            return secoesOrdenadas;
        }

        public async Task<Dictionary<string, bool>> ObterStatusSecoesAsync()
        {
            await GarantirSecoesCardapioParaProdutosAsync();

            var status = await _context.SecoesCardapio
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Nome, s => s.Ativa, StringComparer.OrdinalIgnoreCase);

            var secoesDosProdutos = await _context.Produtos
                .AsNoTracking()
                .Select(p => p.Secao)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToListAsync();

            foreach (var secao in secoesDosProdutos)
            {
                var nomeNormalizado = NormalizarSecao(secao);
                if (!status.ContainsKey(nomeNormalizado))
                {
                    status[nomeNormalizado] = true;
                }
            }

            return status;
        }

        public async Task<SecaoCardapio?> ObterSecaoCardapioPorNomeAsync(string nomeSecao)
        {
            await GarantirSecoesCardapioParaProdutosAsync();

            var nomeNormalizado = NormalizarSecao(nomeSecao);
            return await _context.SecoesCardapio
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.EmpresaId == _context.EmpresaId && s.Nome == nomeNormalizado);
        }

        public async Task<bool> AtualizarSecaoCardapioAsync(
            string nomeAtual,
            string novoNome,
            bool ativa,
            int? ordemNoCardapioInformada = null)
        {
            await GarantirSecoesCardapioParaProdutosAsync();

            var nomeAtualNormalizado = NormalizarSecao(nomeAtual);
            var novoNomeNormalizado = NormalizarSecao(novoNome);

            var secao = await _context.SecoesCardapio
                .FirstOrDefaultAsync(s => s.EmpresaId == _context.EmpresaId && s.Nome == nomeAtualNormalizado);

            if (secao is null)
            {
                return false;
            }

            var nomeEmUso = await _context.SecoesCardapio
                .AnyAsync(s => s.EmpresaId == _context.EmpresaId
                    && s.Id != secao.Id
                    && s.Nome == novoNomeNormalizado);

            if (nomeEmUso)
            {
                throw new InvalidOperationException("Já existe uma seção com esse nome.");
            }

            secao.OrdemNoCardapio = await PrepararOrdemSecaoParaEdicaoAsync(secao, ordemNoCardapioInformada);
            secao.Ativa = ativa;

            if (!string.Equals(nomeAtualNormalizado, novoNomeNormalizado, StringComparison.OrdinalIgnoreCase))
            {
                var produtosDaSecao = await _context.Produtos
                    .Where(p => p.EmpresaId == _context.EmpresaId && p.Secao == nomeAtualNormalizado)
                    .ToListAsync();

                foreach (var produto in produtosDaSecao)
                {
                    produto.Secao = novoNomeNormalizado;
                }

                secao.Nome = novoNomeNormalizado;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Dictionary<string, int>> ObterProximasOrdensProdutoPorSecaoAsync()
        {
            await GarantirSecoesCardapioParaProdutosAsync();

            var secoes = await ObterSecoesAsync();
            var produtos = await _context.Produtos
                .AsNoTracking()
                .Select(p => new { p.Secao, p.OrdemNoCardapio })
                .ToListAsync();

            var proximasOrdens = produtos
                .GroupBy(p => NormalizarSecao(p.Secao), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Max(p => p.OrdemNoCardapio) + 2,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var secao in secoes)
            {
                if (!proximasOrdens.ContainsKey(secao))
                {
                    proximasOrdens[secao] = 1;
                }
            }

            return proximasOrdens;
        }

        public async Task<int> ObterProximaOrdemSecaoSugeridaAsync()
        {
            await GarantirSecoesCardapioParaProdutosAsync();

            var ultimaOrdem = (await _context.SecoesCardapio
                .Where(s => s.Ativa)
                .Select(s => (int?)s.OrdemNoCardapio)
                .MaxAsync()) ?? -1;

            return ultimaOrdem + 2;
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
            IEnumerable<ProdutoOpcaoSecaoInput>? opcoesSecoes = null,
            int? ordemNoCardapioInformada = null,
            int? ordemSecaoNoCardapioInformada = null)
        {
            var secaoNormalizada = NormalizarSecao(secao);
            await GarantirSecaoCardapioAsync(secaoNormalizada, ordemSecaoNoCardapioInformada);

            var produto = new Produto
            {
                EmpresaId = _context.EmpresaId,
                Secao = secaoNormalizada,
                Nome = nome,
                Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
                OrdemNoCardapio = await PrepararOrdemProdutoParaCadastroAsync(secaoNormalizada, ordemNoCardapioInformada),
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
            IEnumerable<ProdutoOpcaoSecaoInput>? opcoesSecoes = null,
            int? ordemNoCardapioInformada = null)
        {
            var produto = await _context.Produtos
                .Include(p => p.OpcoesSecoes)
                    .ThenInclude(s => s.Opcoes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto is null)
            {
                return false;
            }

            var secaoNormalizada = NormalizarSecao(secao);

            await GarantirSecaoCardapioAsync(secaoNormalizada);

            produto.OrdemNoCardapio = await PrepararOrdemProdutoParaEdicaoAsync(produto, secaoNormalizada, ordemNoCardapioInformada);
            produto.Secao = secaoNormalizada;
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

        private async Task<int> PrepararOrdemProdutoParaCadastroAsync(string secao, int? ordemNoCardapioInformada)
        {
            var secaoNormalizada = NormalizarSecao(secao);
            var produtosDaSecao = await _context.Produtos
                .Where(p => p.EmpresaId == _context.EmpresaId && p.Secao == secaoNormalizada)
                .OrderByDescending(p => p.OrdemNoCardapio)
                .ToListAsync();

            var ordemDesejada = ConverterOrdemUiParaPersistencia(ordemNoCardapioInformada, produtosDaSecao.Count);
            if (!ordemDesejada.HasValue)
            {
                return (produtosDaSecao.Select(p => (int?)p.OrdemNoCardapio).Max() ?? -1) + 1;
            }

            foreach (var produtoExistente in produtosDaSecao.Where(p => p.OrdemNoCardapio >= ordemDesejada.Value))
            {
                produtoExistente.OrdemNoCardapio++;
            }

            return ordemDesejada.Value;
        }

        private async Task<int> PrepararOrdemProdutoParaEdicaoAsync(Produto produto, string secaoDestino, int? ordemNoCardapioInformada)
        {
            var secaoDestinoNormalizada = NormalizarSecao(secaoDestino);
            var secaoAtualNormalizada = NormalizarSecao(produto.Secao);
            var mudouSecao = !string.Equals(secaoAtualNormalizada, secaoDestinoNormalizada, StringComparison.OrdinalIgnoreCase);

            if (mudouSecao)
            {
                var produtosDaSecaoOrigem = await _context.Produtos
                    .Where(p => p.EmpresaId == _context.EmpresaId
                        && p.Id != produto.Id
                        && p.Secao == secaoAtualNormalizada
                        && p.OrdemNoCardapio > produto.OrdemNoCardapio)
                    .ToListAsync();

                foreach (var produtoDaOrigem in produtosDaSecaoOrigem)
                {
                    produtoDaOrigem.OrdemNoCardapio--;
                }

                return await PrepararOrdemProdutoParaCadastroAsync(secaoDestinoNormalizada, ordemNoCardapioInformada);
            }

            if (!ordemNoCardapioInformada.HasValue)
            {
                return produto.OrdemNoCardapio;
            }

            var produtosDaSecao = await _context.Produtos
                .Where(p => p.EmpresaId == _context.EmpresaId && p.Secao == secaoDestinoNormalizada && p.Id != produto.Id)
                .ToListAsync();

            var ordemDesejada = ConverterOrdemUiParaPersistencia(ordemNoCardapioInformada, produtosDaSecao.Count);
            if (!ordemDesejada.HasValue || ordemDesejada.Value == produto.OrdemNoCardapio)
            {
                return produto.OrdemNoCardapio;
            }

            if (ordemDesejada.Value < produto.OrdemNoCardapio)
            {
                foreach (var produtoDaSecao in produtosDaSecao.Where(p => p.OrdemNoCardapio >= ordemDesejada.Value && p.OrdemNoCardapio < produto.OrdemNoCardapio))
                {
                    produtoDaSecao.OrdemNoCardapio++;
                }
            }
            else
            {
                foreach (var produtoDaSecao in produtosDaSecao.Where(p => p.OrdemNoCardapio > produto.OrdemNoCardapio && p.OrdemNoCardapio <= ordemDesejada.Value))
                {
                    produtoDaSecao.OrdemNoCardapio--;
                }
            }

            return ordemDesejada.Value;
        }

        private async Task<int> PrepararOrdemSecaoParaEdicaoAsync(SecaoCardapio secao, int? ordemNoCardapioInformada)
        {
            if (!ordemNoCardapioInformada.HasValue)
            {
                return secao.OrdemNoCardapio;
            }

            var outrasSecoes = await _context.SecoesCardapio
                .Where(s => s.EmpresaId == _context.EmpresaId && s.Id != secao.Id)
                .ToListAsync();

            var ordemDesejada = ConverterOrdemUiParaPersistencia(ordemNoCardapioInformada, outrasSecoes.Count);
            if (!ordemDesejada.HasValue || ordemDesejada.Value == secao.OrdemNoCardapio)
            {
                return secao.OrdemNoCardapio;
            }

            if (ordemDesejada.Value < secao.OrdemNoCardapio)
            {
                foreach (var outraSecao in outrasSecoes.Where(s => s.OrdemNoCardapio >= ordemDesejada.Value && s.OrdemNoCardapio < secao.OrdemNoCardapio))
                {
                    outraSecao.OrdemNoCardapio++;
                }
            }
            else
            {
                foreach (var outraSecao in outrasSecoes.Where(s => s.OrdemNoCardapio > secao.OrdemNoCardapio && s.OrdemNoCardapio <= ordemDesejada.Value))
                {
                    outraSecao.OrdemNoCardapio--;
                }
            }

            return ordemDesejada.Value;
        }

        private async Task<Dictionary<string, int>> ObterMapaOrdemSecoesAsync()
        {
            var secoes = await _context.SecoesCardapio
                .Where(s => s.Ativa)
                .OrderBy(s => s.OrdemNoCardapio)
                .ThenBy(s => s.Nome)
                .ToListAsync();

            return secoes.ToDictionary(
                s => NormalizarSecao(s.Nome),
                s => s.OrdemNoCardapio,
                StringComparer.OrdinalIgnoreCase);
        }

        private async Task GarantirSecoesCardapioParaProdutosAsync()
        {
            var secoesDosProdutos = await _context.Produtos
                .Select(p => p.Secao)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToListAsync();

            var secoesExistentes = await _context.SecoesCardapio
                .Select(s => s.Nome)
                .ToListAsync();

            var nomesExistentes = new HashSet<string>(
                secoesExistentes.Select(NormalizarSecao),
                StringComparer.OrdinalIgnoreCase);

            var houveAlteracao = false;

            foreach (var secao in secoesDosProdutos.Select(NormalizarSecao).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (nomesExistentes.Contains(secao))
                {
                    continue;
                }

                await GarantirSecaoCardapioAsync(secao);
                nomesExistentes.Add(secao);
                houveAlteracao = true;
            }

            if (houveAlteracao)
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task<SecaoCardapio> GarantirSecaoCardapioAsync(string secao, int? ordemNoCardapioInformada = null)
        {
            var secaoNormalizada = NormalizarSecao(secao);
            var secaoExistente = await _context.SecoesCardapio
                .FirstOrDefaultAsync(s => s.Nome == secaoNormalizada);

            if (secaoExistente is not null)
            {
                if (!secaoExistente.Ativa)
                {
                    secaoExistente.Ativa = true;
                }

                return secaoExistente;
            }

            var secoesAtivas = await _context.SecoesCardapio
                .Where(s => s.Ativa)
                .OrderByDescending(s => s.OrdemNoCardapio)
                .ToListAsync();

            var ordemDesejada = ConverterOrdemUiParaPersistencia(ordemNoCardapioInformada, secoesAtivas.Count);
            if (ordemDesejada.HasValue)
            {
                foreach (var secaoParaDeslocar in secoesAtivas.Where(s => s.OrdemNoCardapio >= ordemDesejada.Value))
                {
                    secaoParaDeslocar.OrdemNoCardapio++;
                }
            }

            var ultimaOrdem = secoesAtivas.Select(s => (int?)s.OrdemNoCardapio).Max() ?? -1;
            var ordemAtribuida = ordemDesejada ?? (ultimaOrdem + 1);

            var novaSecao = new SecaoCardapio
            {
                EmpresaId = _context.EmpresaId,
                Nome = secaoNormalizada,
                OrdemNoCardapio = ordemAtribuida,
                Ativa = true
            };

            _context.SecoesCardapio.Add(novaSecao);
            return novaSecao;
        }

        private async Task<int> ObterProximaOrdemProdutoNaSecaoAsync(string secao, int? produtoIdIgnorar = null)
        {
            var secaoNormalizada = NormalizarSecao(secao);
            var query = _context.Produtos
                .Where(p => p.EmpresaId == _context.EmpresaId && p.Secao == secaoNormalizada);

            if (produtoIdIgnorar.HasValue)
            {
                query = query.Where(p => p.Id != produtoIdIgnorar.Value);
            }

            var ultimaOrdem = (await query
                .Select(p => (int?)p.OrdemNoCardapio)
                .MaxAsync()) ?? -1;

            return ultimaOrdem + 1;
        }

        private static int? ConverterOrdemUiParaPersistencia(int? ordemInformada, int quantidadeExistente)
        {
            if (!ordemInformada.HasValue)
            {
                return null;
            }

            return Math.Clamp(ordemInformada.Value - 1, 0, quantidadeExistente);
        }

        private static string NormalizarSecao(string? secao)
        {
            return string.IsNullOrWhiteSpace(secao) ? "Sem seção" : secao.Trim();
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