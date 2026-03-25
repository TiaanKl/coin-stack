using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkBucketToCategoryAndDropBucketColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "Buckets");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Buckets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buckets_CategoryId",
                table: "Buckets",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Buckets_Categories_CategoryId",
                table: "Buckets",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buckets_Categories_CategoryId",
                table: "Buckets");

            migrationBuilder.DropIndex(
                name: "IX_Buckets_CategoryId",
                table: "Buckets");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Buckets");

            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "Buckets",
                type: "TEXT",
                maxLength: 16,
                nullable: true);
        }
    }
}
