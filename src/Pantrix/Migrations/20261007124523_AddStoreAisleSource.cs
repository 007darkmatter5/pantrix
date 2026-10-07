using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrix.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreAisleSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AisleSource",
                table: "Stores",
                type: "TEXT",
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "SearchUrl",
                table: "Stores",
                type: "TEXT",
                nullable: true);

            // Stores that already exist get what a new store of the same chain would: H-E-B is the one chain with
            // browser lookup at this point (see StoreCatalog).
            migrationBuilder.Sql("""
                UPDATE "Stores"
                SET "AisleSource" = 'Browser', "SearchUrl" = 'https://www.heb.com/search?q={item}'
                WHERE replace(replace(replace(lower("Stores"."Name"), '-', ''), '.', ''), ' ', '') LIKE 'heb%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AisleSource",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "SearchUrl",
                table: "Stores");
        }
    }
}
