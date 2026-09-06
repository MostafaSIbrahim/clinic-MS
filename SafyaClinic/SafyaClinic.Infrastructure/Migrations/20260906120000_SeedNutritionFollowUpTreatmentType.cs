using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedNutritionFollowUpTreatmentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DbSeeder.SeedTreatmentTypesAsync only ever runs when the TreatmentTypes table
            // is completely empty, so it never reaches already-running installs. Insert the
            // new zero-cost "Follow-up" (Nutritional) treatment type here instead, guarded so
            // this is safe to run more than once and won't duplicate the row if it was already
            // seeded (e.g. on a brand-new database created after this migration was added).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [TreatmentTypes] WHERE [TypeName] = N'Follow-up' AND [Category] = N'Nutritional')
BEGIN
    INSERT INTO [TreatmentTypes] ([Category], [TypeName], [Description], [DefaultCost], [DurationMinutes], [IsActive], [CreatedAt])
    VALUES (N'Nutritional', N'Follow-up', N'Nutrition follow-up visit — included in the enrollment package, no extra charge', 0.00, 15, 1, GETUTCDATE());
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [TreatmentTypes]
WHERE [TypeName] = N'Follow-up' AND [Category] = N'Nutritional' AND [DefaultCost] = 0.00;
");
        }
    }
}
