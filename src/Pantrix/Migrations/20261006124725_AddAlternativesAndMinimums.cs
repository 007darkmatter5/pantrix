using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrix.Migrations
{
    /// <inheritdoc />
    public partial class AddAlternativesAndMinimums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinimumQuantity",
                table: "Ingredients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecipeIngredientAlternatives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeIngredientId = table.Column<int>(type: "INTEGER", nullable: false),
                    IngredientId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredientAlternatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientAlternatives_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeIngredientAlternatives_RecipeIngredients_RecipeIngredientId",
                        column: x => x.RecipeIngredientId,
                        principalTable: "RecipeIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientAlternatives_IngredientId",
                table: "RecipeIngredientAlternatives",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientAlternatives_RecipeIngredientId",
                table: "RecipeIngredientAlternatives",
                column: "RecipeIngredientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeIngredientAlternatives");

            migrationBuilder.DropColumn(
                name: "MinimumQuantity",
                table: "Ingredients");
        }
    }
}
