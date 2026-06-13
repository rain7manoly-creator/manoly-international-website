using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManolyWarehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxPositionsToShelves : Migration
    {
        // Measured capacity for the D/E/F racks. PLACEHOLDER — update once each
        // rack is physically measured. If racks differ from one another, split
        // this into per-label backfills (e.g. D=8, E=10, F=12) instead.
        private const int RackMaxPositions = 12;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the column NOT NULL DEFAULT 6 — existing rows (A/B/C shelves and
            // any D/E/F rows) all default to 6, so A/B/C are unaffected.
            migrationBuilder.AddColumn<int>(
                name: "MaxPositions",
                table: "Shelves",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            // Backfill the racks to their measured capacity.
            migrationBuilder.Sql($"""
                UPDATE "Shelves"
                SET "MaxPositions" = {RackMaxPositions}
                WHERE "Label" IN ('D', 'E', 'F');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxPositions",
                table: "Shelves");
        }
    }
}
