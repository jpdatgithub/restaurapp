using Restaurapp.BlazorServer.Services;

namespace Restaurapp.BlazorServer.Models
{
    public class ProdutoOpcaoEdicaoModel
    {
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public decimal PrecoDelta { get; set; }
        public int QuantidadeMin { get; set; }
        public int QuantidadeMax { get; set; } = 1;
        public int? Inclusos { get; set; }
        public bool Ativa { get; set; } = true;

        public ProdutoOpcaoEdicaoModel Clone() => new()
        {
            Nome = Nome,
            Descricao = Descricao,
            PrecoDelta = PrecoDelta,
            QuantidadeMin = QuantidadeMin,
            QuantidadeMax = QuantidadeMax,
            Inclusos = Inclusos,
            Ativa = Ativa
        };

        public ProdutoService.ProdutoOpcaoInput ToInput() => new(
            Nome,
            Descricao,
            PrecoDelta,
            QuantidadeMin,
            QuantidadeMax,
            Inclusos,
            Ativa);

        public static ProdutoOpcaoEdicaoModel FromEntity(ProdutoOpcao opcao) => new()
        {
            Nome = opcao.Nome,
            Descricao = opcao.Descricao,
            PrecoDelta = opcao.PrecoDelta,
            QuantidadeMin = opcao.QuantidadeMin,
            QuantidadeMax = opcao.QuantidadeMax,
            Inclusos = opcao.Inclusos,
            Ativa = opcao.Ativa
        };
    }

    public class ProdutoOpcaoSecaoEdicaoModel
    {
        public string Nome { get; set; } = string.Empty;
        public int MinSelecoes { get; set; }
        public int MaxSelecoes { get; set; }
        public bool PermitirQuantidade { get; set; } = true;
        public bool Ativa { get; set; } = true;
        public List<ProdutoOpcaoEdicaoModel> Opcoes { get; set; } = new();

        public ProdutoOpcaoSecaoEdicaoModel Clone() => new()
        {
            Nome = Nome,
            MinSelecoes = MinSelecoes,
            MaxSelecoes = MaxSelecoes,
            PermitirQuantidade = PermitirQuantidade,
            Ativa = Ativa,
            Opcoes = Opcoes.Select(o => o.Clone()).ToList()
        };

        public ProdutoService.ProdutoOpcaoSecaoInput ToInput() => new(
            Nome,
            MinSelecoes,
            MaxSelecoes,
            PermitirQuantidade,
            Ativa,
            Opcoes.Select(o => o.ToInput()).ToList());

        public static List<ProdutoOpcaoSecaoEdicaoModel> FromEntities(IEnumerable<ProdutoOpcaoSecao>? secoes)
        {
            if (secoes is null)
            {
                return new List<ProdutoOpcaoSecaoEdicaoModel>();
            }

            return secoes
                .OrderBy(s => s.Ordem)
                .Select(secao => new ProdutoOpcaoSecaoEdicaoModel
                {
                    Nome = secao.Nome,
                    MinSelecoes = secao.MinSelecoes,
                    MaxSelecoes = secao.MaxSelecoes,
                    PermitirQuantidade = secao.PermitirQuantidade,
                    Ativa = secao.Ativa,
                    Opcoes = secao.Opcoes
                        .OrderBy(o => o.Ordem)
                        .Select(ProdutoOpcaoEdicaoModel.FromEntity)
                        .ToList()
                })
                .ToList();
        }
    }
}