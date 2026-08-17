using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportFingerprint",
                table: "Transactions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Transactions",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<int>(
                name: "StatementImportId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StatementImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    BankFormat = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AccountNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    PeriodStartUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodEndUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RowsParsed = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionsImported = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicatesSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    TransfersSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementImports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportFingerprint",
                table: "Transactions",
                column: "ImportFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_StatementImportId",
                table: "Transactions",
                column: "StatementImportId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_StatementImports_StatementImportId",
                table: "Transactions",
                column: "StatementImportId",
                principalTable: "StatementImports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_StatementImports_StatementImportId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "StatementImports");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ImportFingerprint",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_StatementImportId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ImportFingerprint",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "StatementImportId",
                table: "Transactions");
        }
    }
}
