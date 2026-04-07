using Microsoft.AspNetCore.Identity;
using Restaurapp.BlazorServer.Models;

public class ApplicationUser : IdentityUser
{
    public string? Nome { get; set; }
    public int EmpresaId { get; set; }

    public Empresa Empresa { get; set; }
}
