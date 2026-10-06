using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrix.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ingredients_Name",
                table: "Ingredients");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "KitchenId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KitchenId",
                table: "Stores",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KitchenId",
                table: "ShoppingLists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KitchenId",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KitchenId",
                table: "MealPlans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KitchenId",
                table: "Ingredients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Kitchens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    JoinCode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kitchens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kitchens_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Everything from before kitchens existed goes into one kitchen, so an upgrade loses nothing. This has
            // to run here, after the columns exist and before the foreign keys below are added: those rebuild each
            // table, and every row must already point at a real kitchen by then.
            //
            // With an account already present, that account owns the kitchen and becomes the admin. With data but
            // no account yet, the kitchen has no owner and the first account to be created takes it over.
            migrationBuilder.Sql("""
                INSERT INTO "Kitchens" ("Id", "Name", "OwnerId", "JoinCode")
                SELECT 1,
                       COALESCE((SELECT "UserName" || '''s kitchen' FROM "Users" ORDER BY "Id" LIMIT 1), 'My kitchen'),
                       (SELECT MIN("Id") FROM "Users"),
                       upper(hex(randomblob(4)))
                WHERE EXISTS (SELECT 1 FROM "Users") OR EXISTS (SELECT 1 FROM "Ingredients") OR EXISTS (SELECT 1 FROM "Recipes")
                   OR EXISTS (SELECT 1 FROM "MealPlans") OR EXISTS (SELECT 1 FROM "ShoppingLists") OR EXISTS (SELECT 1 FROM "Stores");

                UPDATE "Ingredients" SET "KitchenId" = 1;
                UPDATE "Recipes" SET "KitchenId" = 1;
                UPDATE "MealPlans" SET "KitchenId" = 1;
                UPDATE "ShoppingLists" SET "KitchenId" = 1;
                UPDATE "Stores" SET "KitchenId" = 1;

                UPDATE "Users" SET "KitchenId" = 1;
                UPDATE "Users" SET "IsAdmin" = 1 WHERE "Id" = (SELECT MIN("Id") FROM "Users");

                -- Every account owns a kitchen to go back to. Any beyond the first keep working in the shared one.
                INSERT INTO "Kitchens" ("Name", "OwnerId", "JoinCode")
                SELECT "Users"."UserName" || '''s kitchen', "Users"."Id", upper(hex(randomblob(4)))
                FROM "Users"
                WHERE "Users"."Id" <> (SELECT MIN("Id") FROM "Users");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_KitchenId",
                table: "Users",
                column: "KitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_KitchenId",
                table: "Stores",
                column: "KitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_KitchenId",
                table: "ShoppingLists",
                column: "KitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_KitchenId",
                table: "Recipes",
                column: "KitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_KitchenId",
                table: "MealPlans",
                column: "KitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_KitchenId_Name",
                table: "Ingredients",
                columns: new[] { "KitchenId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kitchens_JoinCode",
                table: "Kitchens",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kitchens_OwnerId",
                table: "Kitchens",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Kitchens_KitchenId",
                table: "Ingredients",
                column: "KitchenId",
                principalTable: "Kitchens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlans_Kitchens_KitchenId",
                table: "MealPlans",
                column: "KitchenId",
                principalTable: "Kitchens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Kitchens_KitchenId",
                table: "Recipes",
                column: "KitchenId",
                principalTable: "Kitchens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingLists_Kitchens_KitchenId",
                table: "ShoppingLists",
                column: "KitchenId",
                principalTable: "Kitchens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Kitchens_KitchenId",
                table: "Stores",
                column: "KitchenId",
                principalTable: "Kitchens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Kitchens_KitchenId",
                table: "Users",
                column: "KitchenId",
                principalTable: "Kitchens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_Kitchens_KitchenId",
                table: "Ingredients");

            migrationBuilder.DropForeignKey(
                name: "FK_MealPlans_Kitchens_KitchenId",
                table: "MealPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Kitchens_KitchenId",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingLists_Kitchens_KitchenId",
                table: "ShoppingLists");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Kitchens_KitchenId",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Kitchens_KitchenId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Kitchens");

            migrationBuilder.DropIndex(
                name: "IX_Users_KitchenId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Stores_KitchenId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingLists_KitchenId",
                table: "ShoppingLists");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_KitchenId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_MealPlans_KitchenId",
                table: "MealPlans");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_KitchenId_Name",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KitchenId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KitchenId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "KitchenId",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "KitchenId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "KitchenId",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "KitchenId",
                table: "Ingredients");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Name",
                table: "Ingredients",
                column: "Name",
                unique: true);
        }
    }
}
