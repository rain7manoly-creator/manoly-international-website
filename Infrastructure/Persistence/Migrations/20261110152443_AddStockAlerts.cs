using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManolyWarehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlertAt",
                table: "StockItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AlertAt",
                table: "Products",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertAt",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "AlertAt",
                table: "Products");
        }
    }
}
