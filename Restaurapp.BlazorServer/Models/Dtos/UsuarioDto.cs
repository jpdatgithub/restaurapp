namespace Restaurapp.BlazorServer.Models.Dtos;

public class UsuarioDto
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public int EmpresaId { get; set; }
    public bool EmailConfirmed { get; set; }
}