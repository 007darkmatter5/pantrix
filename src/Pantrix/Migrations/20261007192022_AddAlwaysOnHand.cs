using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrix.Migrations
{
    /// <inheritdoc />
    public partial class AddAlwaysOnHand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlwaysOnHand",
                table: "Ingredients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlwaysOnHand",
                table: "Ingredients");
        }
    }
}
