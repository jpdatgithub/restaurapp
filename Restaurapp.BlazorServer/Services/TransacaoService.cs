using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Data;
using Restaurapp.BlazorServer.Models;

namespace Restaurapp.BlazorServer.Services
{
    public class TransacaoService
    {
        private readonly AppDbContext _context;

        public TransacaoService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Transacao>> ObterTransacoesAsync()
        {
            return await _context.Transacoes
                .OrderByDescending(t => t.DataDeCadastro)
                .ToListAsync();
        }

        public async Task<List<Transacao>> ObterReceitasAsync()
        {
            return await _context.Transacoes
                .Where(t => t.Categoria == CategoriaDeTransacao.Receita)
                .OrderByDescending(t => t.DataDeCadastro)
                .ToListAsync();
        }

        public async Task<List<Transacao>> ObterDespesasAsync()
        {
            return await _context.Transacoes
                .Where(t => t.Categoria == CategoriaDeTransacao.Despesa)
                .OrderByDescending(t => t.DataDeCadastro)
                .ToListAsync();
        }

        public async Task<Transacao> CadastrarDespesaAsync(string descricao, decimal valor, DateTime? dataRetroativa)
        {
            var despesa = new Transacao
            {
                EmpresaId = _context.EmpresaId,
                Categoria = CategoriaDeTransacao.Despesa,
                Descricao = descricao,
                Valor = valor,
                DataDeCadastro = DateTime.UtcNow,
                DataRetroativa = ConverterParaUtc(dataRetroativa)
            };

            _context.Transacoes.Add(despesa);
            await _context.SaveChangesAsync();

            return despesa;
        }

        public async Task<Transacao> CadastrarReceitaAsync(string descricao, decimal valor, DateTime? dataRetroativa)
        {
            var receita = new Transacao
            {
                EmpresaId = _context.EmpresaId,
                Categoria = CategoriaDeTransacao.Receita,
                Descricao = descricao,
                Valor = valor,
                DataDeCadastro = DateTime.UtcNow,
                DataRetroativa = ConverterParaUtc(dataRetroativa)
            };

            _context.Transacoes.Add(receita);
            await _context.SaveChangesAsync();

            return receita;
        }

        private static DateTime? ConverterParaUtc(DateTime? data)
        {
            if (data is null)
            {
                return null;
            }

            return data.Value.Kind switch
            {
                DateTimeKind.Utc => data.Value,
                DateTimeKind.Local => data.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(data.Value, DateTimeKind.Local).ToUniversalTime()
            };
        }
    }
}