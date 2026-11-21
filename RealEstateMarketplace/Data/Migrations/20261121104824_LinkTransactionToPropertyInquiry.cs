using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateMarketplace.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkTransactionToPropertyInquiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PropertyInquiryId",
                table: "Transaction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransactionType",
                table: "Transaction",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_PropertyInquiryId",
                table: "Transaction",
                column: "PropertyInquiryId",
                unique: true,
                filter: "[PropertyInquiryId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Transaction_PropertyInquiries_PropertyInquiryId",
                table: "Transaction",
                column: "PropertyInquiryId",
                principalTable: "PropertyInquiries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transaction_PropertyInquiries_PropertyInquiryId",
                table: "Transaction");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_PropertyInquiryId",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "PropertyInquiryId",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "Transaction");
        }
    }
}
