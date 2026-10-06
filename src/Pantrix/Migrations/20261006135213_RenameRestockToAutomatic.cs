using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrix.Migrations
{
    /// <inheritdoc />
    public partial class RenameRestockToAutomatic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsRestock",
                table: "ShoppingListItems",
                newName: "IsAutomatic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsAutomatic",
                table: "ShoppingListItems",
                newName: "IsRestock");
        }
    }
}
