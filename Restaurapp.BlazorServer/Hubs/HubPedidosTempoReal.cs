using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Restaurapp.BlazorServer.Hubs
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class HubPedidosTempoReal : Hub
    {
        public async Task EntrarGrupoEmpresa(int empresaId)
        {
            var claimEmpresa = Context.User?.FindFirst("EmpresaId")?.Value;
            if (!int.TryParse(claimEmpresa, out var empresaClaimId) || empresaClaimId != empresaId)
            {
                throw new HubException("Acesso negado para grupo da empresa.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, ObterGrupoEmpresa(empresaId));
        }

        public async Task EntrarGrupoCliente(int clienteUsuarioId)
        {
            var claimCliente = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(claimCliente, out var clienteClaimId) || clienteClaimId != clienteUsuarioId)
            {
                throw new HubException("Acesso negado para grupo do cliente.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, ObterGrupoCliente(clienteUsuarioId));
        }

        public static string ObterGrupoEmpresa(int empresaId) => $"empresa:{empresaId}";
        public static string ObterGrupoCliente(int clienteUsuarioId) => $"cliente:{clienteUsuarioId}";
    }
}