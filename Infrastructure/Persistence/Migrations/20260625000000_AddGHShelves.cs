using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ManolyWarehouse.Infrastructure.Persistence;

#nullable disable

namespace ManolyWarehouse.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260625000000_AddGHShelves")]
    public partial class AddGHShelves : Migration
    {
        // G and H sit on top of the D/E/F rack (top-to-bottom: H, G, D, E, F).
        // Mirror the rack capacity used for D/E/F.
        private const int RackMaxPositions = 12;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Numbers 1..76 — same union as D/E/F (1..69 base + 70..76 rack tops).
            var values = new System.Text.StringBuilder();
            bool first = true;
            for (int n = 1; n <= 76; n++)
            {
                foreach (var label in new[] { "G", "H" })
                {
                    if (!first) values.Append(",\n");
                    values.Append($"    ('{label}{n}', '{label}', {n}, 'DEF', {RackMaxPositions})");
                    first = false;
                }
            }

            migrationBuilder.Sql($"""
                INSERT INTO "Shelves" ("Code", "Label", "Number", "Side", "MaxPositions") VALUES
                {values}
                ON CONFLICT ("Code") DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "Shelves" WHERE "Label" IN ('G', 'H');
                """);
        }
    }
}
