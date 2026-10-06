using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrix.Migrations
{
    /// <inheritdoc />
    public partial class AddStorePlaceIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Stores",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Stores",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsmPlaceId",
                table: "Stores",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "OsmPlaceId",
                table: "Stores");
        }
    }
}
