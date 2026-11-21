using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateMarketplace.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateListingTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transaction_PropertyListingId",
                table: "Transaction");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_PropertyListingId",
                table: "Transaction",
                column: "PropertyListingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transaction_PropertyListingId",
                table: "Transaction");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_PropertyListingId",
                table: "Transaction",
                column: "PropertyListingId");
        }
    }
}
