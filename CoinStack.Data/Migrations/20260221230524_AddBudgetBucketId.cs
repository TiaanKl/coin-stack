using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetBucketId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BucketId",
                table: "Budgets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_BucketId",
                table: "Budgets",
                column: "BucketId");

            migrationBuilder.AddForeignKey(
                name: "FK_Budgets_Buckets_BucketId",
                table: "Budgets",
                column: "BucketId",
                principalTable: "Buckets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Budgets_Buckets_BucketId",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_BucketId",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "BucketId",
                table: "Budgets");
        }
    }
}
