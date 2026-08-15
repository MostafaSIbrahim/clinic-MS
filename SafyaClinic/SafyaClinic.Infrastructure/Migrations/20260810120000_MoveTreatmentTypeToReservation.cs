using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveTreatmentTypeToReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Remove TreatmentType from the clinical Treatment log ──
            migrationBuilder.DropForeignKey(
                name: "FK_Treatments_TreatmentTypes_TreatmentTypeId",
                table: "Treatments");

            migrationBuilder.DropIndex(
                name: "IX_Treatments_TreatmentTypeId",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "TreatmentTypeId",
                table: "Treatments");

            // ── Add TreatmentType to Reservation instead ───────────────
            // Default existing rows to TreatmentTypeId 1 ("General Consultation",
            // seeded in SafyaDbContext) so the new NOT NULL FK doesn't break rows
            // that already exist in the database.
            migrationBuilder.AddColumn<int>(
                name: "TreatmentTypeId",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_TreatmentTypeId",
                table: "Reservations",
                column: "TreatmentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_TreatmentTypes_TreatmentTypeId",
                table: "Reservations",
                column: "TreatmentTypeId",
                principalTable: "TreatmentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_TreatmentTypes_TreatmentTypeId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_TreatmentTypeId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "TreatmentTypeId",
                table: "Reservations");

            migrationBuilder.AddColumn<int>(
                name: "TreatmentTypeId",
                table: "Treatments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_TreatmentTypeId",
                table: "Treatments",
                column: "TreatmentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Treatments_TreatmentTypes_TreatmentTypeId",
                table: "Treatments",
                column: "TreatmentTypeId",
                principalTable: "TreatmentTypes",
                principalColumn: "Id");
        }
    }
}
