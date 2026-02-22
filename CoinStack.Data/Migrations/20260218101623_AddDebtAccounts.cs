using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDebtAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DebtAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MonthlyPaymentAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    PaymentStartDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlannedTermMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DebtAccounts_Name",
                table: "DebtAccounts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DebtAccounts_PaymentStartDateUtc",
                table: "DebtAccounts",
                column: "PaymentStartDateUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DebtAccounts");
        }
    }
}
