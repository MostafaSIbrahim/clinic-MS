using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class correctWeightColumnName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "weight",
                table: "Patients",
                newName: "Weight");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Weight",
                table: "Patients",
                newName: "weight");
        }
    }
}
