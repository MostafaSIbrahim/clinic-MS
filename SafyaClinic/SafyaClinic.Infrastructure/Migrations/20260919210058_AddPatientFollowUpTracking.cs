using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientFollowUpTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FollowUpDismissalReason",
                table: "PatientRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpReservationId",
                table: "PatientRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpStatus",
                table: "PatientRecords",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpUpdatedAtUtc",
                table: "PatientRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpUpdatedBy",
                table: "PatientRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientRecords_FollowUpReservationId",
                table: "PatientRecords",
                column: "FollowUpReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientRecords_FollowUpStatus_FollowUpDate_DoctorId",
                table: "PatientRecords",
                columns: new[] { "FollowUpStatus", "FollowUpDate", "DoctorId" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientRecords_FollowUpUpdatedBy",
                table: "PatientRecords",
                column: "FollowUpUpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientRecords_Reservations_FollowUpReservationId",
                table: "PatientRecords",
                column: "FollowUpReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientRecords_Users_FollowUpUpdatedBy",
                table: "PatientRecords",
                column: "FollowUpUpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientRecords_Reservations_FollowUpReservationId",
                table: "PatientRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientRecords_Users_FollowUpUpdatedBy",
                table: "PatientRecords");

            migrationBuilder.DropIndex(
                name: "IX_PatientRecords_FollowUpReservationId",
                table: "PatientRecords");

            migrationBuilder.DropIndex(
                name: "IX_PatientRecords_FollowUpStatus_FollowUpDate_DoctorId",
                table: "PatientRecords");

            migrationBuilder.DropIndex(
                name: "IX_PatientRecords_FollowUpUpdatedBy",
                table: "PatientRecords");

            migrationBuilder.DropColumn(
                name: "FollowUpDismissalReason",
                table: "PatientRecords");

            migrationBuilder.DropColumn(
                name: "FollowUpReservationId",
                table: "PatientRecords");

            migrationBuilder.DropColumn(
                name: "FollowUpStatus",
                table: "PatientRecords");

            migrationBuilder.DropColumn(
                name: "FollowUpUpdatedAtUtc",
                table: "PatientRecords");

            migrationBuilder.DropColumn(
                name: "FollowUpUpdatedBy",
                table: "PatientRecords");
        }
    }
}
