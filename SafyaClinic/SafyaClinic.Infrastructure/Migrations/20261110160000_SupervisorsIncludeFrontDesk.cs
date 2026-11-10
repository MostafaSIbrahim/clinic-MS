using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SafyaClinic.Infrastructure.Data;

#nullable disable

namespace SafyaClinic.Infrastructure.Migrations
{
    /// <summary>
    /// P8a (data only, no schema change). The built-in "Reception supervisors" and "Clinic managers" groups
    /// now hold everything Reception has, so one group is enough per person. The start-up seeder only adds
    /// permissions that are new to the catalogue, so existing databases get the front-desk grants here.
    /// On an empty database the groups don't exist yet and this changes nothing; the seeder creates them
    /// with the full set. The codes are written out so this migration never changes later.
    /// Hand-written: the model is unchanged, so there's no target-model snapshot.
    /// </summary>
    [DbContext(typeof(SafyaDbContext))]
    [Migration("20261110160000_SupervisorsIncludeFrontDesk")]
    public partial class SupervisorsIncludeFrontDesk : Migration
    {
        private const string Codes =
            "(N'Patients.View'), (N'Patients.Edit'), " +
            "(N'Reservations.View'), (N'Reservations.Manage'), (N'Reservations.ViewQueue'), " +
            "(N'Reservations.ViewAllQueues'), (N'Reservations.CheckIn'), (N'Reservations.ChangeAnyDoctorStatus'), " +
            "(N'Medicines.Search'), " +
            "(N'FollowUps.View'), (N'FollowUps.Manage'), (N'FollowUps.ViewAll')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "INSERT INTO [GroupPermissions] ([GroupId], [PermissionCode]) " +
                "SELECT g.[Id], c.[Code] FROM [PermissionGroups] g " +
                $"CROSS JOIN (VALUES {Codes}) AS c([Code]) " +
                "WHERE g.[SystemKey] IN (N'ReceptionAdmin', N'ClinicAdmin') " +
                "AND EXISTS (SELECT 1 FROM [Permissions] p WHERE p.[Code] = c.[Code]) " +
                "AND NOT EXISTS (SELECT 1 FROM [GroupPermissions] gp WHERE gp.[GroupId] = g.[Id] AND gp.[PermissionCode] = c.[Code]);");

            migrationBuilder.Sql(
                "UPDATE [PermissionGroups] SET [Description] = N'Everything Reception does, plus write-offs and doctors'' calendars' " +
                "WHERE [SystemKey] = N'ReceptionAdmin' AND [Description] = N'Payments, payment reports, write-offs and doctors'' calendars';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE gp FROM [GroupPermissions] gp JOIN [PermissionGroups] g ON g.[Id] = gp.[GroupId] " +
                $"WHERE g.[SystemKey] IN (N'ReceptionAdmin', N'ClinicAdmin') AND gp.[PermissionCode] IN (SELECT c.[Code] FROM (VALUES {Codes}) AS c([Code]));");

            migrationBuilder.Sql(
                "UPDATE [PermissionGroups] SET [Description] = N'Payments, payment reports, write-offs and doctors'' calendars' " +
                "WHERE [SystemKey] = N'ReceptionAdmin' AND [Description] = N'Everything Reception does, plus write-offs and doctors'' calendars';");
        }
    }
}
