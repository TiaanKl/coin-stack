using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBucketGoalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoalId",
                table: "Buckets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buckets_GoalId",
                table: "Buckets",
                column: "GoalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Buckets_Goals_GoalId",
                table: "Buckets",
                column: "GoalId",
                principalTable: "Goals",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buckets_Goals_GoalId",
                table: "Buckets");

            migrationBuilder.DropIndex(
                name: "IX_Buckets_GoalId",
                table: "Buckets");

            migrationBuilder.DropColumn(
                name: "GoalId",
                table: "Buckets");
        }
    }
}
