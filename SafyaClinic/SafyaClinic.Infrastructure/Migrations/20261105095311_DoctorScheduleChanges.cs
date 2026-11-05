using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DoctorScheduleChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DoctorScheduleChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    IsDayOff = table.Column<bool>(type: "bit", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "time(0)", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorScheduleChanges", x => x.Id);
                    table.CheckConstraint("CK_DoctorScheduleChanges_Period", "([IsDayOff] = 1 AND [Kind] IS NULL AND [StartTime] IS NULL AND [EndTime] IS NULL) OR ([IsDayOff] = 0 AND [Kind] IS NOT NULL AND [StartTime] < [EndTime])");
                    table.ForeignKey(
                        name: "FK_DoctorScheduleChanges_DoctorSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "DoctorSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorScheduleChanges_ScheduleId_Date",
                table: "DoctorScheduleChanges",
                columns: new[] { "ScheduleId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorScheduleChanges");
        }
    }
}
