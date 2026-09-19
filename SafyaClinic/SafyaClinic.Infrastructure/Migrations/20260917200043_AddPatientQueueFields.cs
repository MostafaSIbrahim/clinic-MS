using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientQueueFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedInAtUtc",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsultationStartedAtUtc",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueueEndedAtUtc",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueueStatus",
                table: "Reservations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotCheckedIn");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ClinicId_DoctorId_QueueStatus_CheckedInAtUtc",
                table: "Reservations",
                columns: new[] { "ClinicId", "DoctorId", "QueueStatus", "CheckedInAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_ClinicId_DoctorId_QueueStatus_CheckedInAtUtc",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CheckedInAtUtc",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ConsultationStartedAtUtc",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "QueueEndedAtUtc",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "QueueStatus",
                table: "Reservations");
        }
    }
}
