using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "Categories",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Both");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Categories");
        }
    }
}
