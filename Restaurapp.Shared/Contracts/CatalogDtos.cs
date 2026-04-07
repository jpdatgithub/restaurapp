namespace Restaurapp.Shared.Contracts;

public class EmpresaPublicaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool HabilitarContasPosPagas { get; set; }
}

public class ProdutoCatalogoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string? ImagemUrl { get; set; }
}

public class EmpresaCatalogoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool HabilitarContasPosPagas { get; set; }
    public List<ProdutoCatalogoDto> Produtos { get; set; } = new();
}
