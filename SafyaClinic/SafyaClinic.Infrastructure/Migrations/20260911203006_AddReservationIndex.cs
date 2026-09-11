using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DoctorId_ReservationDate_ReservationTime_Id",
                table: "Reservations",
                columns: new[] { "DoctorId", "ReservationDate", "ReservationTime", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_DoctorId_ReservationDate_ReservationTime_Id",
                table: "Reservations");
        }
    }
}
