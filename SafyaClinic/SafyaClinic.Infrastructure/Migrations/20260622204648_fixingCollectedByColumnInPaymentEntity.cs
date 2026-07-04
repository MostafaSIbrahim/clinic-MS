using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class fixingCollectedByColumnInPaymentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_CollectorId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CollectorId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CollectorId",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CollectedBy",
                table: "Payments",
                column: "CollectedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_CollectedBy",
                table: "Payments",
                column: "CollectedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_CollectedBy",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CollectedBy",
                table: "Payments");

            migrationBuilder.AddColumn<int>(
                name: "CollectorId",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CollectorId",
                table: "Payments",
                column: "CollectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_CollectorId",
                table: "Payments",
                column: "CollectorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
