using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateMarketplace.Data.Migrations
{
    /// <inheritdoc />
    public partial class SavedSearchMapArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Furnished",
                table: "SavedSearches",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasParking",
                table: "SavedSearches",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "SavedSearches",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "SavedSearches",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RadiusKm",
                table: "SavedSearches",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Furnished",
                table: "SavedSearches");

            migrationBuilder.DropColumn(
                name: "HasParking",
                table: "SavedSearches");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "SavedSearches");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "SavedSearches");

            migrationBuilder.DropColumn(
                name: "RadiusKm",
                table: "SavedSearches");
        }
    }
}
