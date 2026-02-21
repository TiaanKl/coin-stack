using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SavingsIsPercent",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySavingsAmount",
                table: "AppSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlySavingsPercent",
                table: "AppSettings",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SavingsInterestRate",
                table: "AppSettings",
                type: "decimal(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SavingsInterestIsYearly",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SavingsIsPercent",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "MonthlySavingsAmount",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "MonthlySavingsPercent",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SavingsInterestRate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SavingsInterestIsYearly",
                table: "AppSettings");
        }
    }
}
