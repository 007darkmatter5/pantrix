using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrix.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IngredientId",
                table: "MealPlanEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "MealPlanEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "MealPlanEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: "Each");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_IngredientId",
                table: "MealPlanEntries",
                column: "IngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanEntries_Ingredients_IngredientId",
                table: "MealPlanEntries",
                column: "IngredientId",
                principalTable: "Ingredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanEntries_Ingredients_IngredientId",
                table: "MealPlanEntries");

            migrationBuilder.DropIndex(
                name: "IX_MealPlanEntries_IngredientId",
                table: "MealPlanEntries");

            migrationBuilder.DropColumn(
                name: "IngredientId",
                table: "MealPlanEntries");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "MealPlanEntries");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "MealPlanEntries");
        }
    }
}
