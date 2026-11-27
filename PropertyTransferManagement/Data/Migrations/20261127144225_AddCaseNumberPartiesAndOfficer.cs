using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyTransferManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseNumberPartiesAndOfficer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedOfficerId",
                table: "TransferCases",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerEmail",
                table: "TransferCases",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BuyerFullName",
                table: "TransferCases",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CaseNumber",
                table: "TransferCases",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PropertyType",
                table: "TransferCases",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerEmail",
                table: "TransferCases",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SellerFullName",
                table: "TransferCases",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SizeSquareMeters",
                table: "TransferCases",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "TransferCases",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferCases_AssignedOfficerId",
                table: "TransferCases",
                column: "AssignedOfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferCases_CaseNumber",
                table: "TransferCases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferCases_AspNetUsers_AssignedOfficerId",
                table: "TransferCases",
                column: "AssignedOfficerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferCases_AspNetUsers_AssignedOfficerId",
                table: "TransferCases");

            migrationBuilder.DropIndex(
                name: "IX_TransferCases_AssignedOfficerId",
                table: "TransferCases");

            migrationBuilder.DropIndex(
                name: "IX_TransferCases_CaseNumber",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "AssignedOfficerId",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "BuyerEmail",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "BuyerFullName",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "CaseNumber",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "PropertyType",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "SellerEmail",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "SellerFullName",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "SizeSquareMeters",
                table: "TransferCases");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                table: "TransferCases");
        }
    }
}
