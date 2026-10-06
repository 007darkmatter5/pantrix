using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrix.Migrations
{
    /// <inheritdoc />
    public partial class AddStoresAndAisles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "ShoppingLists",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreAisles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StoreId = table.Column<int>(type: "INTEGER", nullable: false),
                    IngredientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Aisle = table.Column<string>(type: "TEXT", nullable: false),
                    LastConfirmed = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreAisles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreAisles_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreAisles_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_StoreId",
                table: "ShoppingLists",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreAisles_IngredientId",
                table: "StoreAisles",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreAisles_StoreId_IngredientId",
                table: "StoreAisles",
                columns: new[] { "StoreId", "IngredientId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingLists_Stores_StoreId",
                table: "ShoppingLists",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingLists_Stores_StoreId",
                table: "ShoppingLists");

            migrationBuilder.DropTable(
                name: "StoreAisles");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingLists_StoreId",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "ShoppingLists");
        }
    }
}
