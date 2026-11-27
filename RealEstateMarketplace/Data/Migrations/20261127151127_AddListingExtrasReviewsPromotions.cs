using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateMarketplace.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingExtrasReviewsPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccuracyRating",
                table: "PropertyReviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommunicationRating",
                table: "PropertyReviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConditionRating",
                table: "PropertyReviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeaturedUntil",
                table: "PropertyListings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentMonths",
                table: "PropertyListings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InstallmentsAccepted",
                table: "PropertyListings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RentalPeriodMonths",
                table: "PropertyListings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "PropertyListings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasBalcony",
                table: "Properties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasGarden",
                table: "Properties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasTerrace",
                table: "Properties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFurnished",
                table: "Properties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OtherFacilities",
                table: "Properties",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkingSpaces",
                table: "Properties",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccuracyRating",
                table: "PropertyReviews");

            migrationBuilder.DropColumn(
                name: "CommunicationRating",
                table: "PropertyReviews");

            migrationBuilder.DropColumn(
                name: "ConditionRating",
                table: "PropertyReviews");

            migrationBuilder.DropColumn(
                name: "FeaturedUntil",
                table: "PropertyListings");

            migrationBuilder.DropColumn(
                name: "InstallmentMonths",
                table: "PropertyListings");

            migrationBuilder.DropColumn(
                name: "InstallmentsAccepted",
                table: "PropertyListings");

            migrationBuilder.DropColumn(
                name: "RentalPeriodMonths",
                table: "PropertyListings");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "PropertyListings");

            migrationBuilder.DropColumn(
                name: "HasBalcony",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "HasGarden",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "HasTerrace",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IsFurnished",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "OtherFacilities",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ParkingSpaces",
                table: "Properties");
        }
    }
}
