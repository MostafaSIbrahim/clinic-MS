using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VisitsDocumentedInRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MedicalRecordId",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_MedicalRecordId",
                table: "Reservations",
                column: "MedicalRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_PatientRecords_MedicalRecordId",
                table: "Reservations",
                column: "MedicalRecordId",
                principalTable: "PatientRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(@"UPDATE r SET r.MedicalRecordId = pr.Id FROM Reservations r INNER JOIN PatientRecords pr ON pr.ReservationId = r.Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_PatientRecords_MedicalRecordId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_MedicalRecordId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "MedicalRecordId",
                table: "Reservations");
        }
    }
}
