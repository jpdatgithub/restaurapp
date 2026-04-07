using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurapp.BlazorServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMinutosVisibilidadeFilaClienteComerAqui : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinutosVisibilidadeFilaClienteComerAqui",
                table: "Empresas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinutosVisibilidadeFilaClienteComerAqui",
                table: "Empresas",
                type: "integer",
                nullable: false,
                defaultValue: 10);
        }
    }
}
