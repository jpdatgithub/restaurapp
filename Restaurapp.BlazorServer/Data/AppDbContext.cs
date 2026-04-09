using Microsoft.EntityFrameworkCore;
using Restaurapp.BlazorServer.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Restaurapp.BlazorServer.Services;
using Microsoft.AspNetCore.Identity;

namespace Restaurapp.BlazorServer.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public int EmpresaId { get; private set; }

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            IProvedorDeTenantService? provedorDeTenant = null) : base(options)
        {
            if (provedorDeTenant?.TemTenant == true)
            {
                EmpresaId = provedorDeTenant.EmpresaId;
            }
        }

        public void SetTenant(int empresaId)
        {
            EmpresaId = empresaId;
        }
        public DbSet<MagicRegisterToken> MagicRegisterTokens { get; set; }
        public DbSet<Transacao> Transacoes { get; set; }
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<ProdutoOpcaoSecao> ProdutoOpcoesSecoes { get; set; }
        public DbSet<ProdutoOpcao> ProdutoOpcoes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<ContaMesa> ContasMesa { get; set; }
        public DbSet<ItemDePedido> ItensDePedido { get; set; }
        public DbSet<ItemDePedidoOpcaoSnapshot> ItensDePedidoOpcoesSnapshots { get; set; }
        public DbSet<HistoricoStatusPedido> HistoricosStatusPedidos { get; set; }
        public DbSet<ClienteUsuario> ClientesUsuarios { get; set; }
        public DbSet<Empresa> Empresas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // ----------------------
            // GLOBAL QUERY FILTERS (MULTI-TENANT)
            // ----------------------

            modelBuilder.Entity<Transacao>()
                .HasQueryFilter(t => t.EmpresaId == EmpresaId);

            modelBuilder.Entity<Produto>()
                .HasQueryFilter(p => p.EmpresaId == EmpresaId);

            modelBuilder.Entity<Pedido>()
                .HasQueryFilter(p => p.EmpresaId == EmpresaId);

            modelBuilder.Entity<ContaMesa>()
                .HasQueryFilter(c => c.EmpresaId == EmpresaId);

            modelBuilder.Entity<ItemDePedido>()
                .HasQueryFilter(i => i.Pedido != null && i.Pedido.EmpresaId == EmpresaId);

            modelBuilder.Entity<HistoricoStatusPedido>()
                .HasQueryFilter(h => h.Pedido != null && h.Pedido.EmpresaId == EmpresaId);


            // ----------------------
            // APPLICATION USER
            // ----------------------
            modelBuilder.Entity<ApplicationUser>()
            .HasOne(u => u.Empresa)
            .WithMany()
            .HasForeignKey(u => u.EmpresaId);

            // ----------------------
            // CONFIGURAÇÕES DE EMPRESA
            // ----------------------
            modelBuilder.Entity<Empresa>()
                .Property(e => e.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Empresa>()
                .Property(e => e.HabilitarContasPosPagas)
                .HasDefaultValue(false)
                .IsRequired();

            // ----------------------
            // TRANSAÇÕES
            // ----------------------
            modelBuilder.Entity<Transacao>(entity =>
            {
                entity.ToTable("Transacoes");

                entity.HasKey(t => t.Id);

                entity.Property(t => t.Descricao)
                    .IsRequired()
                    .HasMaxLength(300);

                entity.Property(t => t.Valor)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(t => t.DataDeCadastro)
                    .IsRequired();

                entity.Property(t => t.Categoria)
                    .IsRequired();
            });

            // ----------------------
            // PRODUTOS
            // ----------------------
            modelBuilder.Entity<Produto>(entity =>
            {
                entity.ToTable("Produtos");

                entity.HasKey(p => p.Id);

                entity.Property(p => p.Nome)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.Secao)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(p => p.Descricao)
                    .HasMaxLength(500);

                entity.Property(p => p.Preco)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(p => p.ImagemUrl)
                    .HasMaxLength(500);

                entity.HasMany(p => p.OpcoesSecoes)
                    .WithOne(s => s.Produto)
                    .HasForeignKey(s => s.ProdutoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProdutoOpcaoSecao>(entity =>
            {
                entity.ToTable("ProdutosOpcoesSecoes");

                entity.HasKey(s => s.Id);

                entity.Property(s => s.Nome)
                    .IsRequired()
                    .HasMaxLength(120);

                entity.Property(s => s.MinSelecoes)
                    .IsRequired();

                entity.Property(s => s.MaxSelecoes)
                    .IsRequired();

                entity.Property(s => s.PermitirQuantidade)
                    .IsRequired();

                entity.Property(s => s.Ativa)
                    .IsRequired();

                entity.HasIndex(s => new { s.ProdutoId, s.Ordem });
            });

            modelBuilder.Entity<ProdutoOpcao>(entity =>
            {
                entity.ToTable("ProdutosOpcoes");

                entity.HasKey(o => o.Id);

                entity.Property(o => o.Nome)
                    .IsRequired()
                    .HasMaxLength(120);

                entity.Property(o => o.Descricao)
                    .HasMaxLength(300);

                entity.Property(o => o.PrecoDelta)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(o => o.QuantidadeMin)
                    .IsRequired();

                entity.Property(o => o.QuantidadeMax)
                    .IsRequired();

                entity.Property(o => o.Inclusos)
                    .IsRequired(false);

                entity.Property(o => o.Ativa)
                    .IsRequired();

                entity.HasOne(o => o.Secao)
                    .WithMany(s => s.Opcoes)
                    .HasForeignKey(o => o.ProdutoOpcaoSecaoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(o => new { o.ProdutoOpcaoSecaoId, o.Ordem });
            });

            // ----------------------
            // PEDIDOS
            // ----------------------
            modelBuilder.Entity<Pedido>(entity =>
            {
                entity.ToTable("Pedidos");

                entity.HasKey(p => p.Id);

                entity.Property(p => p.NomeClienteSnapshot)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.EmailClienteSnapshot)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.TipoAtendimento)
                    .IsRequired();

                entity.Property(p => p.Origem)
                    .IsRequired();

                entity.Property(p => p.NumeroMesa)
                    .HasMaxLength(50);

                entity.HasOne(p => p.ContaMesa)
                    .WithMany(c => c.Pedidos)
                    .HasForeignKey(p => p.ContaMesaId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(p => p.Moeda)
                    .IsRequired()
                    .HasMaxLength(10);

                entity.Property(p => p.Subtotal)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(p => p.Desconto)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(p => p.Frete)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(p => p.Total)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(p => p.EnderecoEntrega)
                    .HasMaxLength(500);

                entity.Property(p => p.Observacoes)
                    .HasMaxLength(500);

                entity.Property(p => p.CreatedAtUtc)
                    .IsRequired();

                entity.Property(p => p.UpdatedAtUtc)
                    .IsRequired();

                entity.Property(p => p.Status)
                    .IsRequired();
            });

            modelBuilder.Entity<ItemDePedido>(entity =>
            {
                entity.ToTable("ItensDePedido");

                entity.HasKey(i => i.Id);

                entity.Property(i => i.NomeProdutoSnapshot)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(i => i.PrecoUnitarioSnapshot)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(i => i.SubtotalItem)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(i => i.Quantidade)
                    .IsRequired();

                entity.HasOne(i => i.Pedido)
                    .WithMany(p => p.Itens)
                    .HasForeignKey(i => i.PedidoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ItemDePedidoOpcaoSnapshot>(entity =>
            {
                entity.ToTable("ItensDePedidoOpcoesSnapshots");

                entity.HasKey(o => o.Id);

                entity.Property(o => o.NomeSecaoSnapshot)
                    .IsRequired()
                    .HasMaxLength(120);

                entity.Property(o => o.NomeOpcaoSnapshot)
                    .IsRequired()
                    .HasMaxLength(120);

                entity.Property(o => o.PrecoUnitarioDeltaSnapshot)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(o => o.SubtotalDeltaSnapshot)
                    .HasPrecision(18, 2)
                    .IsRequired();

                entity.Property(o => o.QuantidadeInclusa)
                    .IsRequired();

                entity.Property(o => o.QuantidadeCobradaExtra)
                    .IsRequired();

                entity.HasOne(o => o.ItemDePedido)
                    .WithMany(i => i.OpcoesSelecionadas)
                    .HasForeignKey(o => o.ItemDePedidoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ContaMesa>(entity =>
            {
                entity.ToTable("ContasMesa");

                entity.HasKey(c => c.Id);

                entity.Property(c => c.NumeroMesa)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(c => c.Status)
                    .IsRequired();

                entity.Property(c => c.CreatedAtUtc)
                    .IsRequired();

                entity.Property(c => c.UpdatedAtUtc)
                    .IsRequired();

                entity.HasIndex(c => new { c.ClienteUsuarioId, c.Status });
                entity.HasIndex(c => new { c.EmpresaId, c.NumeroMesa, c.Status });
            });

            modelBuilder.Entity<HistoricoStatusPedido>(entity =>
            {
                entity.ToTable("HistoricosStatusPedidos");

                entity.HasKey(h => h.Id);

                entity.Property(h => h.Status)
                    .IsRequired();

                entity.Property(h => h.DataStatusUtc)
                    .IsRequired();

                entity.Property(h => h.RegistradoPor)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasOne(h => h.Pedido)
                    .WithMany(p => p.HistoricoStatus)
                    .HasForeignKey(h => h.PedidoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ----------------------
            // CLIENTES (CATÁLOGO)
            // ----------------------
            modelBuilder.Entity<ClienteUsuario>(entity =>
            {
                entity.ToTable("ClientesUsuarios");

                entity.HasKey(c => c.Id);

                entity.Property(c => c.Nome)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.Email)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.HasIndex(c => c.Email)
                    .IsUnique();

                entity.Property(c => c.SenhaHash)
                    .IsRequired();

                entity.Property(c => c.DataCriacaoUtc)
                    .IsRequired();
            });

            // ----------------------
            // MAGIC REGISTER TOKEN
            // ----------------------
            modelBuilder.Entity<MagicRegisterToken>(entity =>
            {
                entity.ToTable("MagicRegisterTokens");

                entity.HasKey(t => t.Id);

                entity.Property(t => t.Token)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasIndex(t => t.Token);

                entity.Property(t => t.EmpresaId)
                    .IsRequired(false);
            });

            // ----------------------
            // SEED DE ROLES
            // ----------------------
            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = "1",
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = "1"
                },
                new IdentityRole
                {
                    Id = "2",
                    Name = "Regular",
                    NormalizedName = "REGULAR",
                    ConcurrencyStamp = "2"
                }
            );
        }
    }
}
