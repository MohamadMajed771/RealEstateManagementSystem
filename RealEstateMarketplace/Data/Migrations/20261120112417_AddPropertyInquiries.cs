using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateMarketplace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyInquiries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyReviews_Transactions_TransactionId",
                table: "PropertyReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_AspNetUsers_BuyerId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_AspNetUsers_SellerId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_PropertyListings_PropertyListingId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Transactions_TransactionId",
                table: "TransferRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Transactions",
                table: "Transactions");

            migrationBuilder.RenameTable(
                name: "Transactions",
                newName: "Transaction");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_SellerId",
                table: "Transaction",
                newName: "IX_Transaction_SellerId");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_PropertyListingId",
                table: "Transaction",
                newName: "IX_Transaction_PropertyListingId");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_BuyerId",
                table: "Transaction",
                newName: "IX_Transaction_BuyerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transaction",
                table: "Transaction",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "PropertyInquiries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyListingId = table.Column<int>(type: "int", nullable: false),
                    RequesterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyInquiries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyInquiries_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PropertyInquiries_AspNetUsers_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PropertyInquiries_PropertyListings_PropertyListingId",
                        column: x => x.PropertyListingId,
                        principalTable: "PropertyListings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyInquiries_OwnerId",
                table: "PropertyInquiries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyInquiries_PropertyListingId",
                table: "PropertyInquiries",
                column: "PropertyListingId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyInquiries_RequesterId",
                table: "PropertyInquiries",
                column: "RequesterId");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyReviews_Transaction_TransactionId",
                table: "PropertyReviews",
                column: "TransactionId",
                principalTable: "Transaction",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transaction_AspNetUsers_BuyerId",
                table: "Transaction",
                column: "BuyerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transaction_AspNetUsers_SellerId",
                table: "Transaction",
                column: "SellerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transaction_PropertyListings_PropertyListingId",
                table: "Transaction",
                column: "PropertyListingId",
                principalTable: "PropertyListings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Transaction_TransactionId",
                table: "TransferRequests",
                column: "TransactionId",
                principalTable: "Transaction",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyReviews_Transaction_TransactionId",
                table: "PropertyReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Transaction_AspNetUsers_BuyerId",
                table: "Transaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Transaction_AspNetUsers_SellerId",
                table: "Transaction");

            migrationBuilder.DropForeignKey(
                name: "FK_Transaction_PropertyListings_PropertyListingId",
                table: "Transaction");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Transaction_TransactionId",
                table: "TransferRequests");

            migrationBuilder.DropTable(
                name: "PropertyInquiries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Transaction",
                table: "Transaction");

            migrationBuilder.RenameTable(
                name: "Transaction",
                newName: "Transactions");

            migrationBuilder.RenameIndex(
                name: "IX_Transaction_SellerId",
                table: "Transactions",
                newName: "IX_Transactions_SellerId");

            migrationBuilder.RenameIndex(
                name: "IX_Transaction_PropertyListingId",
                table: "Transactions",
                newName: "IX_Transactions_PropertyListingId");

            migrationBuilder.RenameIndex(
                name: "IX_Transaction_BuyerId",
                table: "Transactions",
                newName: "IX_Transactions_BuyerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transactions",
                table: "Transactions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyReviews_Transactions_TransactionId",
                table: "PropertyReviews",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_AspNetUsers_BuyerId",
                table: "Transactions",
                column: "BuyerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_AspNetUsers_SellerId",
                table: "Transactions",
                column: "SellerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_PropertyListings_PropertyListingId",
                table: "Transactions",
                column: "PropertyListingId",
                principalTable: "PropertyListings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Transactions_TransactionId",
                table: "TransferRequests",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id");
        }
    }
}
