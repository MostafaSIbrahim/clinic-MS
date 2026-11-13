using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PatientPlanAndExtraItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PackageItemId",
                table: "WeeklyAdministeredItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CustomName",
                table: "WeeklyAdministeredItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InjectionId",
                table: "WeeklyAdministeredItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "WeeklyAdministeredItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VitaminId",
                table: "WeeklyAdministeredItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnrollmentPlannedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    WeekNumber = table.Column<int>(type: "int", nullable: false),
                    PackageItemId = table.Column<int>(type: "int", nullable: true),
                    InjectionId = table.Column<int>(type: "int", nullable: true),
                    VitaminId = table.Column<int>(type: "int", nullable: true),
                    CustomName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentPlannedItems", x => x.Id);
                    table.CheckConstraint("CK_EnrollmentPlannedItems_OneItem", "(CASE WHEN [PackageItemId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [InjectionId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [VitaminId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [CustomName] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_EnrollmentPlannedItems_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_EnrollmentPlannedItems_Week", "[WeekNumber] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_EnrollmentPlannedItems_InjectionTypes_InjectionId",
                        column: x => x.InjectionId,
                        principalTable: "InjectionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentPlannedItems_PackageItems_PackageItemId",
                        column: x => x.PackageItemId,
                        principalTable: "PackageItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentPlannedItems_PatientNutritionEnrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "PatientNutritionEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentPlannedItems_VitaminTypes_VitaminId",
                        column: x => x.VitaminId,
                        principalTable: "VitaminTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAdministeredItems_InjectionId",
                table: "WeeklyAdministeredItems",
                column: "InjectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAdministeredItems_VitaminId",
                table: "WeeklyAdministeredItems",
                column: "VitaminId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WeeklyAdministeredItems_OneItem",
                table: "WeeklyAdministeredItems",
                sql: "(CASE WHEN [PackageItemId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [InjectionId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [VitaminId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [CustomName] IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPlannedItems_EnrollmentId_WeekNumber",
                table: "EnrollmentPlannedItems",
                columns: new[] { "EnrollmentId", "WeekNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPlannedItems_InjectionId",
                table: "EnrollmentPlannedItems",
                column: "InjectionId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPlannedItems_PackageItemId",
                table: "EnrollmentPlannedItems",
                column: "PackageItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPlannedItems_VitaminId",
                table: "EnrollmentPlannedItems",
                column: "VitaminId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyAdministeredItems_InjectionTypes_InjectionId",
                table: "WeeklyAdministeredItems",
                column: "InjectionId",
                principalTable: "InjectionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyAdministeredItems_VitaminTypes_VitaminId",
                table: "WeeklyAdministeredItems",
                column: "VitaminId",
                principalTable: "VitaminTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyAdministeredItems_InjectionTypes_InjectionId",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyAdministeredItems_VitaminTypes_VitaminId",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropTable(
                name: "EnrollmentPlannedItems");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyAdministeredItems_InjectionId",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyAdministeredItems_VitaminId",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WeeklyAdministeredItems_OneItem",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropColumn(
                name: "CustomName",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropColumn(
                name: "InjectionId",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropColumn(
                name: "VitaminId",
                table: "WeeklyAdministeredItems");

            migrationBuilder.AlterColumn<int>(
                name: "PackageItemId",
                table: "WeeklyAdministeredItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
