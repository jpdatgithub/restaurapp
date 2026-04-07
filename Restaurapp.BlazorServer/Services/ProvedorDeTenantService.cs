namespace Restaurapp.BlazorServer.Services
{
    public interface IProvedorDeTenantService
    {
        int EmpresaId { get; }
        bool TemTenant { get; }
    }
    public class ProvedorDeTenantService : IProvedorDeTenantService
    {
        public int EmpresaId { get; private set; }
        public bool TemTenant { get; private set; }

        public ProvedorDeTenantService(IHttpContextAccessor http)
        {
            var user = http.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var claim = user.FindFirst("EmpresaId");

                if (claim != null && int.TryParse(claim.Value, out var id))
                {
                    EmpresaId = id;
                    TemTenant = true;
                    return;
                }
            }

            // fallback seguro
            EmpresaId = 0;
            TemTenant = false;
        }
    }
}
