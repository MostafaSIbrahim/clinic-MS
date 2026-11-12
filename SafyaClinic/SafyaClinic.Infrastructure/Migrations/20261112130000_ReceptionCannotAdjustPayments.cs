using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SafyaClinic.Infrastructure.Data;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <summary>
    /// P9a (data only, no schema change). The built-in Reception group no longer holds "Payments.Adjust" (change
    /// or cancel a collected payment); the front desk only collects. Supervisors, clinic managers and admin keep it.
    /// On an empty database the group doesn't exist yet and this changes nothing; the seeder creates it without
    /// the permission. Hand-written: the model is unchanged, so there's no target-model snapshot.
    /// </summary>
    [DbContext(typeof(SafyaDbContext))]
    [Migration("20261112130000_ReceptionCannotAdjustPayments")]
    public partial class ReceptionCannotAdjustPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE gp FROM [GroupPermissions] gp JOIN [PermissionGroups] g ON g.[Id] = gp.[GroupId] " +
                "WHERE g.[SystemKey] = N'Reception' AND gp.[PermissionCode] = N'Payments.Adjust';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "INSERT INTO [GroupPermissions] ([GroupId], [PermissionCode]) " +
                "SELECT g.[Id], N'Payments.Adjust' FROM [PermissionGroups] g " +
                "WHERE g.[SystemKey] = N'Reception' " +
                "AND EXISTS (SELECT 1 FROM [Permissions] p WHERE p.[Code] = N'Payments.Adjust') " +
                "AND NOT EXISTS (SELECT 1 FROM [GroupPermissions] gp WHERE gp.[GroupId] = g.[Id] AND gp.[PermissionCode] = N'Payments.Adjust');");
        }
    }
}
