using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurapp.BlazorServer.Migrations
{
    /// <inheritdoc />
    public partial class AddInclusosProdutoOpcoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Inclusos",
                table: "ProdutosOpcoes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeCobradaExtra",
                table: "ItensDePedidoOpcoesSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeInclusa",
                table: "ItensDePedidoOpcoesSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Inclusos",
                table: "ProdutosOpcoes");

            migrationBuilder.DropColumn(
                name: "QuantidadeCobradaExtra",
                table: "ItensDePedidoOpcoesSnapshots");

            migrationBuilder.DropColumn(
                name: "QuantidadeInclusa",
                table: "ItensDePedidoOpcoesSnapshots");
        }
    }
}
