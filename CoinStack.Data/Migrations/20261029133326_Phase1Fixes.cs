using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Fixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResistedAtUtc",
                table: "WaitlistItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByStatementImportId",
                table: "Subscriptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByStatementImportId",
                table: "Buckets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DismissedRecurringMerchants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NormalizedKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SubscriptionDismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    BucketDismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DismissedRecurringMerchants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DismissedRecurringMerchants_NormalizedKey",
                table: "DismissedRecurringMerchants",
                column: "NormalizedKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DismissedRecurringMerchants");

            migrationBuilder.DropColumn(
                name: "ResistedAtUtc",
                table: "WaitlistItems");

            migrationBuilder.DropColumn(
                name: "CreatedByStatementImportId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CreatedByStatementImportId",
                table: "Buckets");
        }
    }
}
