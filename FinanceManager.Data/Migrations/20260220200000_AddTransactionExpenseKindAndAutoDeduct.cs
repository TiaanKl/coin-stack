using CoinStack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CoinStackDbContext))]
    [Migration("20260220200000_AddTransactionExpenseKindAndAutoDeduct")]
    public partial class AddTransactionExpenseKindAndAutoDeduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpenseKind",
                table: "Transactions",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Discretionary");

            migrationBuilder.AddColumn<bool>(
                name: "AutoDeduct",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoDeductTemplateId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AutoDeductTemplateId",
                table: "Transactions",
                column: "AutoDeductTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_AutoDeductTemplateId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExpenseKind",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AutoDeduct",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AutoDeductTemplateId",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
        }
    }
}
