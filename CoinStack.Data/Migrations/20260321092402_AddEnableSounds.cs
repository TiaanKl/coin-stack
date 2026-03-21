using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnableSounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableSounds",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableSounds",
                table: "AppSettings");
        }
    }
}
