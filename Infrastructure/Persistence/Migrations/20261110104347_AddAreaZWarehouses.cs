using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ManolyWarehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAreaZWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The list of warehouses inside Area Z
            migrationBuilder.CreateTable(
                name: "AreaZWarehouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaZWarehouses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AreaZWarehouses_Name",
                table: "AreaZWarehouses",
                column: "Name",
                unique: true);

            // 2. The first warehouse — everything already in Area Z belongs to it
            migrationBuilder.Sql("""
                INSERT INTO "AreaZWarehouses" ("Name", "SortOrder", "CreatedAt")
                VALUES ('Smirdex Warehouse', 1, now() at time zone 'utc');
                """);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "AreaZInventory",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "AreaZInventory"
                SET "WarehouseId" = (SELECT "Id" FROM "AreaZWarehouses" WHERE "Name" = 'Smirdex Warehouse');
                """);

            migrationBuilder.AlterColumn<int>(
                name: "WarehouseId",
                table: "AreaZInventory",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // 3. One active row per product per warehouse (was: per product)
            migrationBuilder.DropIndex(
                name: "IX_AreaZInventory_ProductId_Active",
                table: "AreaZInventory");

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_AreaZInventory_ProductId" ON "AreaZInventory" ("ProductId");""");

            migrationBuilder.CreateIndex(
                name: "IX_AreaZInventory_Warehouse_Product_Active",
                table: "AreaZInventory",
                columns: new[] { "WarehouseId", "ProductId" },
                unique: true,
                filter: "\"IsDispatched\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_AreaZInventory_AreaZWarehouses_WarehouseId",
                table: "AreaZInventory",
                column: "WarehouseId",
                principalTable: "AreaZWarehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AreaZInventory_AreaZWarehouses_WarehouseId",
                table: "AreaZInventory");

            migrationBuilder.DropTable(
                name: "AreaZWarehouses");

            migrationBuilder.DropIndex(
                name: "IX_AreaZInventory_ProductId",
                table: "AreaZInventory");

            migrationBuilder.DropIndex(
                name: "IX_AreaZInventory_Warehouse_Product_Active",
                table: "AreaZInventory");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "AreaZInventory");

            migrationBuilder.CreateIndex(
                name: "IX_AreaZInventory_ProductId_Active",
                table: "AreaZInventory",
                column: "ProductId",
                unique: true,
                filter: "\"IsDispatched\" = false");
        }
    }
}
