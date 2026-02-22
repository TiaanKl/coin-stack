using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionDebtLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DebtAccountId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_DebtAccountId",
                table: "Transactions",
                column: "DebtAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_DebtAccounts_DebtAccountId",
                table: "Transactions",
                column: "DebtAccountId",
                principalTable: "DebtAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_DebtAccounts_DebtAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_DebtAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DebtAccountId",
                table: "Transactions");
        }
    }
}
