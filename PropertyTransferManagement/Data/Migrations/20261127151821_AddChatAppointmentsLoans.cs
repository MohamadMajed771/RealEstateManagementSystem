using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyTransferManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAppointmentsLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LenderDecision",
                table: "LoanAssistanceRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LenderDecisionAt",
                table: "LoanAssistanceRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LenderName",
                table: "LoanAssistanceRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "CaseConversations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Audience",
                table: "CaseAppointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "CaseAppointments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LenderDecision",
                table: "LoanAssistanceRequests");

            migrationBuilder.DropColumn(
                name: "LenderDecisionAt",
                table: "LoanAssistanceRequests");

            migrationBuilder.DropColumn(
                name: "LenderName",
                table: "LoanAssistanceRequests");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CaseConversations");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "CaseAppointments");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "CaseAppointments");
        }
    }
}
