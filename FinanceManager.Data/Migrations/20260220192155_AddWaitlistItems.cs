using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaitlistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CoolOffPeriod = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CoolOffUntil = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsUnlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmotionAtTimeOfAdding = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    LastEvaluated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPurchased = table.Column<bool>(type: "INTEGER", nullable: false),
                    PurchasedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReflectionNote = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistItems_CoolOffUntil",
                table: "WaitlistItems",
                column: "CoolOffUntil");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistItems_IsPurchased",
                table: "WaitlistItems",
                column: "IsPurchased");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistItems_IsUnlocked",
                table: "WaitlistItems",
                column: "IsUnlocked");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistItems");
        }
    }
}
