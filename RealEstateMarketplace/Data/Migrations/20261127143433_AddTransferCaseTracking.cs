using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateMarketplace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferCaseTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaseNumber",
                table: "TransferRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalStatus",
                table: "TransferRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalStatusUpdatedAt",
                table: "TransferRequests",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaseNumber",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ExternalStatus",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ExternalStatusUpdatedAt",
                table: "TransferRequests");
        }
    }
}
