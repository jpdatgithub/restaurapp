using Restaurapp.ClienteWasm.Models;

namespace Restaurapp.ClienteWasm.Services
{
    public class ContaMesaEstadoService
    {
        private readonly ContaMesaService _contaMesaService;

        public ContaMesaEstadoService(ContaMesaService contaMesaService)
        {
            _contaMesaService = contaMesaService;
        }

        public ContaAbertaResumoDto? ContaAbertaAtual { get; private set; }
        public bool PossuiContaAberta => ContaAbertaAtual is not null;

        public event Action? EstadoAlterado;

        public async Task AtualizarAsync()
        {
            var conta = await _contaMesaService.ObterContaAbertaResumoAsync();
            var mudou = (ContaAbertaAtual?.ContaMesaId ?? 0) != (conta?.ContaMesaId ?? 0)
                || (ContaAbertaAtual?.TotalConta ?? 0m) != (conta?.TotalConta ?? 0m);

            ContaAbertaAtual = conta;

            if (mudou)
            {
                EstadoAlterado?.Invoke();
            }
        }

        public void Limpar()
        {
            ContaAbertaAtual = null;
            EstadoAlterado?.Invoke();
        }
    }
}
