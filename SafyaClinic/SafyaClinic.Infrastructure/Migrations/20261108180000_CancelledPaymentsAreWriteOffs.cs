using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SafyaClinic.Infrastructure.Data;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <summary>
    /// P6a (data only, no schema change). Until now a cancelled payment still covered its visit and was shown
    /// as "written off". From P6 a cancellation is a void, and write-offs have their own status. So that past
    /// visits and reports don't change, every payment cancelled before this migration becomes a write-off.
    /// Hand-written: the model is unchanged (Status is stored as text), so there's no target-model snapshot.
    /// </summary>
    [DbContext(typeof(SafyaDbContext))]
    [Migration("20261108180000_CancelledPaymentsAreWriteOffs")]
    public partial class CancelledPaymentsAreWriteOffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Payments] SET [Status] = N'WrittenOff' WHERE [Status] = N'Cancelled';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Payments] SET [Status] = N'Cancelled' WHERE [Status] = N'WrittenOff';");
        }
    }
}
