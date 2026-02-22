using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavingsFallbackEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AmountUsed = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsFallbackEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingsMonthlySummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Month = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Base = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Interest = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RunningTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsMonthlySummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingsState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Available = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Reserved = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FallbackEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastCalculatedMonth = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsState", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsFallbackEvents_OccurredAtUtc",
                table: "SavingsFallbackEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsMonthlySummaries_Month",
                table: "SavingsMonthlySummaries",
                column: "Month",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavingsFallbackEvents");

            migrationBuilder.DropTable(
                name: "SavingsMonthlySummaries");

            migrationBuilder.DropTable(
                name: "SavingsState");
        }
    }
}
