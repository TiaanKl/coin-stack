using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAiFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAssistantContextSize",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AiAssistantDisplayMode",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AiAssistantGpuLayerCount",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AiAssistantMaxTokens",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AiAssistantModel",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AiAssistantModelPath",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AiAssistantTemperature",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AiRequireActionConfirmation",
                table: "AppSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiAssistantContextSize",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiAssistantDisplayMode",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiAssistantGpuLayerCount",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiAssistantMaxTokens",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiAssistantModel",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AiAssistantModelPath",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AiAssistantTemperature",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "AiRequireActionConfirmation",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
