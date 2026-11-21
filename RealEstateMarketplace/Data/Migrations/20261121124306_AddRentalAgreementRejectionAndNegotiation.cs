using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateMarketplace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalAgreementRejectionAndNegotiation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentalAgreements_TransactionId",
                table: "RentalAgreements");

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "RentalAgreements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedByUserId",
                table: "RentalAgreements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "RentalAgreements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalAgreements_TransactionId",
                table: "RentalAgreements",
                column: "TransactionId",
                unique: true,
                filter: "[Status] IN (1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentalAgreements_TransactionId",
                table: "RentalAgreements");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "RentalAgreements");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "RentalAgreements");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "RentalAgreements");

            migrationBuilder.CreateIndex(
                name: "IX_RentalAgreements_TransactionId",
                table: "RentalAgreements",
                column: "TransactionId",
                unique: true);
        }
    }
}
