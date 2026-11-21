using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ManolyWarehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWoodWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WoodItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ThicknessMm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    LengthCm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    WidthCm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Color = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Area = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    AlertAt = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WoodItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WoodMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WoodItemId = table.Column<int>(type: "integer", nullable: false),
                    Before = table.Column<int>(type: "integer", nullable: false),
                    After = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WoodMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WoodMovements_WoodItems_WoodItemId",
                        column: x => x.WoodItemId,
                        principalTable: "WoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WoodItems_Type",
                table: "WoodItems",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_WoodMovements_WoodItemId_CreatedAt",
                table: "WoodMovements",
                columns: new[] { "WoodItemId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WoodMovements");

            migrationBuilder.DropTable(
                name: "WoodItems");
        }
    }
}
