namespace Restaurapp.Shared.Contracts;

public class EmpresaPublicaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool HabilitarContasPosPagas { get; set; }
}

public class ProdutoOpcaoCatalogoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public decimal PrecoDelta { get; set; }
    public int QuantidadeMin { get; set; }
    public int QuantidadeMax { get; set; }
    public int? Inclusos { get; set; }
}

public class ProdutoOpcaoSecaoCatalogoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public int MinSelecoes { get; set; }
    public int MaxSelecoes { get; set; }
    public bool PermitirQuantidade { get; set; }
    public List<ProdutoOpcaoCatalogoDto> Opcoes { get; set; } = new();
}

public class ProdutoCatalogoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string? ImagemUrl { get; set; }
    public List<ProdutoOpcaoSecaoCatalogoDto> OpcoesSecoes { get; set; } = new();
}

public class EmpresaCatalogoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool HabilitarContasPosPagas { get; set; }
    public List<ProdutoCatalogoDto> Produtos { get; set; } = new();
}
