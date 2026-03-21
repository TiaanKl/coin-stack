using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencyFundAndReserveAwareBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<bool>(
                name: "EnableEmergencyFallback",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowReserveAwareBudget",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmergencyAvailable",
                table: "SavingsState");

            migrationBuilder.DropColumn(
                name: "EmergencyTotal",
                table: "SavingsState");

            migrationBuilder.DropColumn(
                name: "EnableEmergencyFallback",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ShowReserveAwareBudget",
                table: "AppSettings");
        }
    }
}
