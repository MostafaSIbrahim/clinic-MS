using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniquePrimaryPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // F5b: same normal form as PhoneNumbers.Normalize (symbols removed, +20/0020 → 0).
            migrationBuilder.Sql(@"
                UPDATE PatientPhones SET PhoneNumber =
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(PhoneNumber)),' ',''),'-',''),'(',''),')',''),'.',''),'+','');
                UPDATE PatientPhones SET PhoneNumber = '0' + SUBSTRING(PhoneNumber, 5, 50) WHERE PhoneNumber LIKE '0020%';
                UPDATE PatientPhones SET PhoneNumber = '0' + SUBSTRING(PhoneNumber, 3, 50) WHERE PhoneNumber LIKE '20%' AND LEN(PhoneNumber) >= 11;");

            // F5b: exactly one primary per patient — the oldest phone when none is marked, the oldest when several are.
            migrationBuilder.Sql(@"
                UPDATE pp SET IsPrimary = 1 FROM PatientPhones pp
                WHERE pp.Id IN (SELECT MIN(Id) FROM PatientPhones GROUP BY PatientId HAVING SUM(CASE WHEN IsPrimary = 1 THEN 1 ELSE 0 END) = 0);
                UPDATE pp SET IsPrimary = 0 FROM PatientPhones pp
                WHERE pp.IsPrimary = 1 AND EXISTS (SELECT 1 FROM PatientPhones o WHERE o.PatientId = pp.PatientId AND o.IsPrimary = 1 AND o.Id < pp.Id);");
            migrationBuilder.CreateIndex(
                name: "UX_PatientPhones_PrimaryPhoneNumber",
                table: "PatientPhones",
                column: "PhoneNumber",
                unique: true,
                filter: "[IsPrimary] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PatientPhones_PrimaryPhoneNumber",
                table: "PatientPhones");
        }
    }
}
