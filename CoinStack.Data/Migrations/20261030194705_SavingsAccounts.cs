using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class SavingsAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransferTransactionId",
                table: "SavingsFallbackEvents",
                type: "INTEGER",
                nullable: true);

            // Savings and the emergency fund become accounts holding what their figures showed: savings is the
            // free money plus what goals have set aside, and both are dated now, so transfers from here on move
            // them. An account of that kind made by hand already is the real one and is left alone.
            migrationBuilder.Sql(
                """
                INSERT INTO "Accounts" ("Name", "Type", "AccountNumber", "Balance", "BalanceAsOfUtc", "DebtAccountId", "IsDefault", "IsArchived", "SortOrder", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT 'Savings', 'Savings', NULL,
                    CAST(COALESCE((SELECT ROUND(CAST("Available" AS REAL) + CAST("Reserved" AS REAL), 2) FROM "SavingsState" ORDER BY "Id" LIMIT 1), 0) AS TEXT),
                    datetime('now'), NULL, 0, 0,
                    COALESCE((SELECT MAX("SortOrder") FROM "Accounts"), 0) + 1,
                    datetime('now'), datetime('now')
                WHERE NOT EXISTS (SELECT 1 FROM "Accounts" WHERE "Type" = 'Savings');
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Accounts" ("Name", "Type", "AccountNumber", "Balance", "BalanceAsOfUtc", "DebtAccountId", "IsDefault", "IsArchived", "SortOrder", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT 'Emergency fund', 'EmergencyFund', NULL,
                    CAST(COALESCE((SELECT ROUND(CAST("EmergencyAvailable" AS REAL), 2) FROM "SavingsState" ORDER BY "Id" LIMIT 1), 0) AS TEXT),
                    datetime('now'), NULL, 0, 0,
                    COALESCE((SELECT MAX("SortOrder") FROM "Accounts"), 0) + 1,
                    datetime('now'), datetime('now')
                WHERE NOT EXISTS (SELECT 1 FROM "Accounts" WHERE "Type" = 'EmergencyFund');
                """);

            migrationBuilder.DropColumn(
                name: "Available",
                table: "SavingsState");

            migrationBuilder.DropColumn(
                name: "EmergencyAvailable",
                table: "SavingsState");

            migrationBuilder.DropColumn(
                name: "EmergencyTotal",
                table: "SavingsState");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "SavingsState");

            // "Reserve-aware budget" subtracted savings from the bank balance; savings are separate accounts now.
            migrationBuilder.DropColumn(
                name: "ShowReserveAwareBudget",
                table: "AppSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowReserveAwareBudget",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Available",
                table: "SavingsState",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmergencyAvailable",
                table: "SavingsState",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmergencyTotal",
                table: "SavingsState",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "SavingsState",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Back to figures, from the accounts' last entered balances. The accounts stay; going back past
            // the Accounts migration removes them.
            migrationBuilder.Sql(
                """
                UPDATE "SavingsState"
                SET "Available" = CAST(MAX(0, ROUND(COALESCE((SELECT CAST("Balance" AS REAL) FROM "Accounts" WHERE "Type" = 'Savings' ORDER BY "Id" LIMIT 1), 0) - CAST("Reserved" AS REAL), 2)) AS TEXT),
                    "EmergencyAvailable" = CAST(COALESCE((SELECT CAST("Balance" AS REAL) FROM "Accounts" WHERE "Type" = 'EmergencyFund' ORDER BY "Id" LIMIT 1), 0) AS TEXT);
                UPDATE "SavingsState"
                SET "Total" = CAST(ROUND(CAST("Available" AS REAL) + CAST("Reserved" AS REAL), 2) AS TEXT),
                    "EmergencyTotal" = "EmergencyAvailable";
                """);

            migrationBuilder.DropColumn(
                name: "TransferTransactionId",
                table: "SavingsFallbackEvents");
        }
    }
}
