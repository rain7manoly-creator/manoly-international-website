using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManolyWarehouse.Infrastructure.Persistence.Migrations
{
    public partial class AddMissingShelves70 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Shelves" ("Code", "Label", "Number", "Side") VALUES
                    ('D70', 'D', 70, 'DEF'),
                    ('D71', 'D', 71, 'DEF'),
                    ('D72', 'D', 72, 'DEF'),
                    ('D73', 'D', 73, 'DEF'),
                    ('D74', 'D', 74, 'DEF'),
                    ('D75', 'D', 75, 'DEF'),
                    ('D76', 'D', 76, 'DEF'),
                    ('E70', 'E', 70, 'DEF'),
                    ('E71', 'E', 71, 'DEF'),
                    ('E72', 'E', 72, 'DEF'),
                    ('E73', 'E', 73, 'DEF'),
                    ('E74', 'E', 74, 'DEF'),
                    ('E75', 'E', 75, 'DEF'),
                    ('E76', 'E', 76, 'DEF'),
                    ('F70', 'F', 70, 'DEF'),
                    ('F75', 'F', 75, 'DEF'),
                    ('F76', 'F', 76, 'DEF')
                ON CONFLICT ("Code") DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "Shelves" WHERE "Code" IN (
                    'D70','D71','D72','D73','D74','D75','D76',
                    'E70','E71','E72','E73','E74','E75','E76',
                    'F70','F75','F76'
                );
                """);
        }
    }
}
