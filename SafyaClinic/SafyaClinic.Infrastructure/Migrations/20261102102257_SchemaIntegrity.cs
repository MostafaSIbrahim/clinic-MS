using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SchemaIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MedicalAnalyses_AnalysisTypes_AnalysisTypeId1",
                table: "MedicalAnalyses");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientNutritionEnrollments_NutritionPackages_PackageId",
                table: "PatientNutritionEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientNutritionEnrollments_Patients_PatientId",
                table: "PatientNutritionEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientNutritionEnrollments_Users_DoctorId",
                table: "PatientNutritionEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAdjustments_Payments_PaymentId",
                table: "PaymentAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_CollectedBy",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_PatientRecords_PatientRecordId",
                table: "Prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_PatientRecords_RecordId",
                table: "Prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Treatments_PatientRecords_RecordId",
                table: "Treatments");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyAdministeredItems_PackageItems_PackageItemId1",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyFollowUpLabResults_AnalysisTypes_AnalysisTypeId",
                table: "WeeklyFollowUpLabResults");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyAdministeredItems_PackageItemId1",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_PatientRecordId",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_MedicalAnalyses_AnalysisTypeId1",
                table: "MedicalAnalyses");

            migrationBuilder.DropColumn(
                name: "PackageItemId1",
                table: "WeeklyAdministeredItems");

            migrationBuilder.DropColumn(
                name: "PatientRecordId",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "AnalysisTypeId1",
                table: "MedicalAnalyses");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "PrescriptionAttachments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "AnalysisAttachments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientNutritionEnrollments_NutritionPackages_PackageId",
                table: "PatientNutritionEnrollments",
                column: "PackageId",
                principalTable: "NutritionPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientNutritionEnrollments_Patients_PatientId",
                table: "PatientNutritionEnrollments",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientNutritionEnrollments_Users_DoctorId",
                table: "PatientNutritionEnrollments",
                column: "DoctorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAdjustments_Payments_PaymentId",
                table: "PaymentAdjustments",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_CollectedBy",
                table: "Payments",
                column: "CollectedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_PatientRecords_RecordId",
                table: "Prescriptions",
                column: "RecordId",
                principalTable: "PatientRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Treatments_PatientRecords_RecordId",
                table: "Treatments",
                column: "RecordId",
                principalTable: "PatientRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyFollowUpLabResults_AnalysisTypes_AnalysisTypeId",
                table: "WeeklyFollowUpLabResults",
                column: "AnalysisTypeId",
                principalTable: "AnalysisTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientNutritionEnrollments_NutritionPackages_PackageId",
                table: "PatientNutritionEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientNutritionEnrollments_Patients_PatientId",
                table: "PatientNutritionEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientNutritionEnrollments_Users_DoctorId",
                table: "PatientNutritionEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAdjustments_Payments_PaymentId",
                table: "PaymentAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_CollectedBy",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_PatientRecords_RecordId",
                table: "Prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Treatments_PatientRecords_RecordId",
                table: "Treatments");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyFollowUpLabResults_AnalysisTypes_AnalysisTypeId",
                table: "WeeklyFollowUpLabResults");

            migrationBuilder.AddColumn<int>(
                name: "PackageItemId1",
                table: "WeeklyAdministeredItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatientRecordId",
                table: "Prescriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "PrescriptionAttachments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnalysisTypeId1",
                table: "MedicalAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "AnalysisAttachments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAdministeredItems_PackageItemId1",
                table: "WeeklyAdministeredItems",
                column: "PackageItemId1");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PatientRecordId",
                table: "Prescriptions",
                column: "PatientRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalAnalyses_AnalysisTypeId1",
                table: "MedicalAnalyses",
                column: "AnalysisTypeId1");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalAnalyses_AnalysisTypes_AnalysisTypeId1",
                table: "MedicalAnalyses",
                column: "AnalysisTypeId1",
                principalTable: "AnalysisTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientNutritionEnrollments_NutritionPackages_PackageId",
                table: "PatientNutritionEnrollments",
                column: "PackageId",
                principalTable: "NutritionPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientNutritionEnrollments_Patients_PatientId",
                table: "PatientNutritionEnrollments",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientNutritionEnrollments_Users_DoctorId",
                table: "PatientNutritionEnrollments",
                column: "DoctorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAdjustments_Payments_PaymentId",
                table: "PaymentAdjustments",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_CollectedBy",
                table: "Payments",
                column: "CollectedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_PatientRecords_PatientRecordId",
                table: "Prescriptions",
                column: "PatientRecordId",
                principalTable: "PatientRecords",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_PatientRecords_RecordId",
                table: "Prescriptions",
                column: "RecordId",
                principalTable: "PatientRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Treatments_PatientRecords_RecordId",
                table: "Treatments",
                column: "RecordId",
                principalTable: "PatientRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyAdministeredItems_PackageItems_PackageItemId1",
                table: "WeeklyAdministeredItems",
                column: "PackageItemId1",
                principalTable: "PackageItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyFollowUpLabResults_AnalysisTypes_AnalysisTypeId",
                table: "WeeklyFollowUpLabResults",
                column: "AnalysisTypeId",
                principalTable: "AnalysisTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
