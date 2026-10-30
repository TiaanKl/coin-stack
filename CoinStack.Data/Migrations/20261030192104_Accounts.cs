using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class Accounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Balance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BalanceAsOfUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DebtAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_DebtAccounts_DebtAccountId",
                        column: x => x.DebtAccountId,
                        principalTable: "DebtAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferAccountId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "StatementImports",
                type: "INTEGER",
                nullable: true);

            // The single bank balance becomes the main account, with the same anchor, and every existing
            // transaction and import is put on it, so the balance shown before and after is the same.
            // Safe to run twice: nothing is inserted once an account exists.
            migrationBuilder.Sql(
                """
                INSERT INTO "Accounts" ("Name", "Type", "AccountNumber", "Balance", "BalanceAsOfUtc", "DebtAccountId", "IsDefault", "IsArchived", "SortOrder", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT 'Main account', 'Cheque',
                    (SELECT "AccountNumber" FROM "StatementImports"
                     WHERE "AccountNumber" IS NOT NULL AND "AccountNumber" <> ''
                     ORDER BY COALESCE("PeriodEndUtc", "CreatedAtUtc") DESC LIMIT 1),
                    COALESCE((SELECT "CurrentBankBalance" FROM "AppSettings" ORDER BY "Id" LIMIT 1), '0.0'),
                    (SELECT "BankBalanceAsOfUtc" FROM "AppSettings" ORDER BY "Id" LIMIT 1),
                    NULL, 1, 0, 0, datetime('now'), datetime('now')
                WHERE NOT EXISTS (SELECT 1 FROM "Accounts");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Transactions"
                SET "AccountId" = (SELECT "Id" FROM "Accounts" WHERE "IsDefault" = 1 ORDER BY "Id" LIMIT 1)
                WHERE "AccountId" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "StatementImports"
                SET "AccountId" = (SELECT "Id" FROM "Accounts" WHERE "IsDefault" = 1 ORDER BY "Id" LIMIT 1)
                WHERE "AccountId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransferAccountId",
                table: "Transactions",
                column: "TransferAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_AccountId",
                table: "StatementImports",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts",
                column: "AccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_DebtAccountId",
                table: "Accounts",
                column: "DebtAccountId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StatementImports_Accounts_AccountId",
                table: "StatementImports",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_TransferAccountId",
                table: "Transactions",
                column: "TransferAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "BankBalanceAsOfUtc",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CurrentBankBalance",
                table: "AppSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BankBalanceAsOfUtc",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBankBalance",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            // The main account's anchor goes back to being the single bank balance.
            migrationBuilder.Sql(
                """
                UPDATE "AppSettings"
                SET "CurrentBankBalance" = COALESCE((SELECT "Balance" FROM "Accounts" WHERE "IsDefault" = 1 ORDER BY "Id" LIMIT 1), '0.0'),
                    "BankBalanceAsOfUtc" = (SELECT "BalanceAsOfUtc" FROM "Accounts" WHERE "IsDefault" = 1 ORDER BY "Id" LIMIT 1);
                """);

            // The account columns go below, but the table rebuilds that drop them run last, after "Accounts" is
            // dropped; let go of the accounts first so dropping the table breaks no reference.
            migrationBuilder.Sql(
                """
                UPDATE "Transactions" SET "AccountId" = NULL, "TransferAccountId" = NULL;
                UPDATE "StatementImports" SET "AccountId" = NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_StatementImports_Accounts_AccountId",
                table: "StatementImports");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_TransferAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransferAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_StatementImports_AccountId",
                table: "StatementImports");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransferAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "StatementImports");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
