using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Restaurapp.BlazorServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCardapioOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrdemNoCardapio",
                table: "Produtos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SecoesCardapio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OrdemNoCardapio = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecoesCardapio", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                WITH produtos_ordenados AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "EmpresaId", COALESCE(NULLIF(BTRIM("Secao"), ''), 'Sem seção')
                               ORDER BY "Id"
                           ) - 1 AS ordem
                    FROM "Produtos"
                )
                UPDATE "Produtos" AS p
                SET "OrdemNoCardapio" = po.ordem
                FROM produtos_ordenados AS po
                WHERE p."Id" = po."Id";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "SecoesCardapio" ("EmpresaId", "Nome", "OrdemNoCardapio", "Ativa")
                SELECT secoes."EmpresaId",
                       secoes."SecaoNormalizada",
                       ROW_NUMBER() OVER (
                           PARTITION BY secoes."EmpresaId"
                           ORDER BY secoes."PrimeiroProdutoId", secoes."SecaoNormalizada"
                       ) - 1 AS "OrdemNoCardapio",
                       TRUE
                FROM (
                    SELECT "EmpresaId",
                           COALESCE(NULLIF(BTRIM("Secao"), ''), 'Sem seção') AS "SecaoNormalizada",
                           MIN("Id") AS "PrimeiroProdutoId"
                    FROM "Produtos"
                    GROUP BY "EmpresaId", COALESCE(NULLIF(BTRIM("Secao"), ''), 'Sem seção')
                ) AS secoes;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SecoesCardapio_EmpresaId_Nome",
                table: "SecoesCardapio",
                columns: new[] { "EmpresaId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecoesCardapio_EmpresaId_OrdemNoCardapio",
                table: "SecoesCardapio",
                columns: new[] { "EmpresaId", "OrdemNoCardapio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecoesCardapio");

            migrationBuilder.DropColumn(
                name: "OrdemNoCardapio",
                table: "Produtos");
        }
    }
}
