# Slice F4a — Permission groups: catalogue, tables and clinician type

Roadmap item **F4**, slice a of 4 (Group 2). Starts decision **D1** (permission groups instead of fixed roles) and fixes the role-ID part of **F06**.

| | |
|---|---|
| Branch | `chore/f4a-permission-groups` (from an up-to-date `main`) |
| Behaviour change | **None for users.** Access is still decided by roles. This slice adds what F4b–F4d build on: 1. a **permission catalogue** in code (39 permissions); 2. four tables for permissions, groups, grants and group membership; 3. **five system groups**, one per role, granting exactly what that role can do today; 4. **`ClinicianType`** on users (None / Doctor / Nutritionist), which replaces the hard-coded role IDs 2 and 5 in the doctor list |
| Database | ⚠ One migration: 4 new tables, 1 new column on `Users`, filled from each user's current roles |
| Size | M |

## Design

| Decision | Choice |
|---|---|
| Where permissions are defined | In code (`PermissionCatalogue`). The seeder copies them to the `Permissions` table at startup. Admins will combine permissions into groups (F4d) but cannot invent new ones, so every permission is one the code really enforces. This avoids the problem of the "Reception Admin" role, which granted nothing (F06) |
| How fine-grained | One permission for each distinct thing a role can do today, read from the 7 policies in `Program.cs`, the `[Authorize]` attributes (controller and action rules combine) and the role checks inside controllers. The full mapping is in the appendix; F4b uses it to switch each action over |
| System groups | One per role in use: **Administrators**, **Doctors**, **Nutritionists**, **Reception**, **Reception supervisors**. `SystemKey` holds the role name. The `Patient` role grants nothing today, so it gets no group |
| Membership until F4d | **Through the role.** Users with the `Doctor` role belong to the group whose `SystemKey` is `Doctor`. So role changes in the Users screen keep working with nothing to keep in step. `UserGroups` stays empty until F4d moves memberships over and retires roles |
| Seeder rules | The catalogue is synced on every start. Each system group is created **once**, with its grants. After that, grants an admin removes stay removed. The exception is **Administrators**, which always receives every permission, including ones added in later releases |
| Clinician type | Decides who appears in doctor pickers and queues. Filled from today's roles **by role name, not ID**. A user with both Doctor and Nutritionist gets **Doctor**. Until F4d it follows the roles: assigning or removing the Doctor or Nutritionist role updates it, and so does creating a user |
| Enforcement | **Not in this slice.** F4b adds `[RequirePermission]` and an access-matrix test that compares old and new access for every action. A test in this slice already pins the grants to today's access table |

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Domain/Enums/ClinicianType.cs` |
| Add | `SafyaClinic/SafyaClinic.Domain/Identity/PermissionGroups.cs` (4 entities) |
| Edit | `SafyaClinic/SafyaClinic.Domain/Identity/User.cs` |
| Add | `SafyaClinic/SafyaClinic.Application/Security/PermissionCatalogue.cs` |
| Add | `SafyaClinic/SafyaClinic.Application/Security/ClinicianTypes.cs` |
| Edit | `SafyaClinic/SafyaClinic.Application/Services/UserService.cs` |
| Add | `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/PermissionGroupConfigurations.cs` |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/UserConfiguration.cs` |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/SafyaDbContext.cs` |
| Edit | `SafyaClinic/SafyaClinic.Infrastructure/Data/DbSeeder.cs` |
| Add | `Migrations/<timestamp>_PermissionGroups.cs` (+ Designer, snapshot): generated, plus SQL you add |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/PermissionCatalogueTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Application/ClinicianTypeTests.cs` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/PermissionSeedingTests.cs` |
| Add | `Docs/implementation/F4a-permission-groups-tables.md`, `Docs/deployment/D1-deploy-group1-and-foundations.md` |

## Risks

| Risk | Mitigation |
|---|---|
| Role IDs in the database aren't 2 = Doctor and 5 = Nutritionist | Step 1 shows the IDs. The new code uses names, so the doctor list becomes correct either way. If they differ, today's list is already wrong; tell me before migrating |
| A grant doesn't match today's access | A test compares every system group with the access table (appendix). F4b adds a per-action comparison before anything is enforced |
| Users with several roles (for example Doctor + Reception) | They're the only users whose access could differ once permissions are enforced, because some of today's checks are written as "Doctor and not Admin". Step 1 lists them, and F4b handles them explicitly |
| Doctor picker changes | Clinician type is filled from the same roles the old query used. Step 9 checks the picker |

## Rollback

Revert the PR's merge commit. On dev, `dotnet ef database update UniquePrimaryPhone` drops the four tables and the column (ask me first). No existing data is changed apart from the new column.

---

## Steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1 — Roles report (read-only): send me the output

```powershell
$cfg = Get-Content SafyaClinic\SafyaClinic.Web\appsettings.json -Raw | ConvertFrom-Json
$conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
$conn.Open()
function Show($sql) { $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $t = New-Object System.Data.DataTable; $t.Load($cmd.ExecuteReader()); $t | Format-Table -AutoSize }
"Roles and how many users have each:"
Show "SELECT r.Id, r.RoleName, COUNT(ur.Id) AS Users FROM Roles r LEFT JOIN UserRoles ur ON ur.RoleId = r.Id GROUP BY r.Id, r.RoleName ORDER BY r.Id"
"Users with more than one role:"
Show "SELECT u.Id, u.FullName, u.IsActive, STRING_AGG(r.RoleName, ', ') AS Roles FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id JOIN Roles r ON r.Id = ur.RoleId GROUP BY u.Id, u.FullName, u.IsActive HAVING COUNT(*) > 1"
$conn.Close()
```

You can continue while I look at it. Expected: Id 2 = `Doctor` and Id 5 = `Nutritionist`. Any other roles (created on the Roles screen) are listed too; they grant nothing today, and F4d deals with them.

### Step 2 — Branch

```powershell
git switch main
git pull
git switch -c chore/f4a-permission-groups
```

### Step 3 — Domain

**3a.** Create `SafyaClinic/SafyaClinic.Domain/Enums/ClinicianType.cs`:

```csharp
namespace SafyaClinic.Domain.Enums
{
    /// <summary>
    /// F4a: who appears in doctor pickers and queues. Replaces the role-ID checks
    /// (2 = Doctor, 5 = Nutritionist). Stored as text.
    /// </summary>
    public enum ClinicianType
    {
        None = 0,
        Doctor = 1,
        Nutritionist = 2
    }
}
```

**3b.** Create `SafyaClinic/SafyaClinic.Domain/Identity/PermissionGroups.cs`:

```csharp
using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Identity
{
    /// <summary>F4a (D1): an action the code can enforce. Defined in code (PermissionCatalogue); the seeder copies it here.</summary>
    public class Permission
    {
        public string Code { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSensitive { get; set; }

        public virtual ICollection<GroupPermission> GroupPermissions { get; set; } = new List<GroupPermission>();
    }

    /// <summary>F4a: a named set of permissions. Admins manage groups from F4d.</summary>
    public class PermissionGroup : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>The role this group stands for until roles are retired (F4d); null for groups an admin creates.</summary>
        public string? SystemKey { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        public virtual ICollection<GroupPermission> GroupPermissions { get; set; } = new List<GroupPermission>();
        public virtual ICollection<UserGroup> Members { get; set; } = new List<UserGroup>();
    }

    /// <summary>F4a: a permission granted to a group.</summary>
    public class GroupPermission
    {
        public int GroupId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;

        public virtual PermissionGroup Group { get; set; } = null!;
        public virtual Permission Permission { get; set; } = null!;
    }

    /// <summary>F4a: a user's membership of a group. Empty until F4d; until then membership comes from the role.</summary>
    public class UserGroup
    {
        public int UserId { get; set; }
        public int GroupId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public int? AssignedBy { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual PermissionGroup Group { get; set; } = null!;
    }
}
```

**3c.** `SafyaClinic/SafyaClinic.Domain/Identity/User.cs`:

- Add with the other `using` lines at the top:

  ```csharp
  using SafyaClinic.Domain.Enums;
  ```

- After the `SecurityStamp` line, add:

  ```csharp
          public ClinicianType ClinicianType { get; set; } = ClinicianType.None;   // F4a: doctor pickers and queues
  ```

- After the `UserRoles` navigation line, add:

  ```csharp
          public virtual ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
  ```

### Step 4 — The catalogue and the system groups (Application)

**4a.** Create `SafyaClinic/SafyaClinic.Application/Security/PermissionCatalogue.cs`:

```csharp
namespace SafyaClinic.Application.Security;

/// <summary>
/// F4a (D1): the permission codes the application can enforce. Admins combine them into
/// groups (F4d) but cannot invent new ones. Enforcement switches to these codes in F4b–F4c.
/// </summary>
public static class Permissions
{
    public static class Patients
    {
        public const string View = "Patients.View";
        public const string Edit = "Patients.Edit";
        public const string ViewTimeline = "Patients.ViewTimeline";
        public const string EditMedical = "Patients.EditMedical";
    }

    public static class Reservations
    {
        public const string View = "Reservations.View";
        public const string Manage = "Reservations.Manage";
        public const string ViewQueue = "Reservations.ViewQueue";
        public const string ViewAllQueues = "Reservations.ViewAllQueues";
        public const string CheckIn = "Reservations.CheckIn";
        public const string StartConsultation = "Reservations.StartConsultation";
        public const string OverridePrice = "Reservations.OverridePrice";
        public const string SetCompleted = "Reservations.SetCompleted";
        public const string ChangeAnyDoctorStatus = "Reservations.ChangeAnyDoctorStatus";
    }

    public static class Records
    {
        public const string View = "Records.View";
        public const string Write = "Records.Write";
        public const string Lock = "Records.Lock";
    }

    public static class Consultations
    {
        public const string ActOnBehalf = "Consultations.ActOnBehalf";
    }

    public static class Medicines
    {
        public const string Search = "Medicines.Search";
    }

    public static class Analyses
    {
        public const string View = "Analyses.View";
        public const string Manage = "Analyses.Manage";
    }

    public static class Attachments
    {
        public const string Delete = "Attachments.Delete";
    }

    public static class FollowUps
    {
        public const string View = "FollowUps.View";
        public const string Manage = "FollowUps.Manage";
        public const string ViewAll = "FollowUps.ViewAll";
    }

    public static class Nutrition
    {
        public const string View = "Nutrition.View";
        public const string Manage = "Nutrition.Manage";
        public const string ManagePackages = "Nutrition.ManagePackages";
    }

    public static class Payments
    {
        public const string View = "Payments.View";
        public const string Collect = "Payments.Collect";
        public const string Adjust = "Payments.Adjust";
        public const string ViewReport = "Payments.ViewReport";
        public const string ReportAnyDate = "Payments.ReportAnyDate";
        public const string ReportAllDoctors = "Payments.ReportAllDoctors";
        public const string ViewFullDashboard = "Payments.ViewFullDashboard";
        public const string Maintenance = "Payments.Maintenance";
    }

    public static class Settings
    {
        public const string Clinics = "Settings.Clinics";
        public const string PatientSources = "Settings.PatientSources";
        public const string Lookups = "Settings.Lookups";
    }

    public static class Users
    {
        public const string Manage = "Users.Manage";
    }
}

public sealed record PermissionDefinition(string Code, string Area, string Description, bool IsSensitive = false);

/// <summary>F4a: every permission with the text shown to admins. The seeder copies this list to the database.</summary>
public static class PermissionCatalogue
{
    public static readonly IReadOnlyList<PermissionDefinition> All = new PermissionDefinition[]
    {
        new(Permissions.Patients.View, "Patients", "See the patient list and patient details; search patients"),
        new(Permissions.Patients.Edit, "Patients", "Register patients; edit basic details, phones and addresses"),
        new(Permissions.Patients.ViewTimeline, "Patients", "See a patient's clinical timeline"),
        new(Permissions.Patients.EditMedical, "Patients", "Edit a patient's medical information"),

        new(Permissions.Reservations.View, "Reservations", "See reservations, today's list, the board and history reports"),
        new(Permissions.Reservations.Manage, "Reservations", "Create, edit and cancel reservations; change their status"),
        new(Permissions.Reservations.ViewQueue, "Reservations", "Open the waiting queue"),
        new(Permissions.Reservations.ViewAllQueues, "Reservations", "See every doctor's queue, not only their own"),
        new(Permissions.Reservations.CheckIn, "Reservations", "Check patients in"),
        new(Permissions.Reservations.StartConsultation, "Reservations", "Start a consultation from a reservation"),
        new(Permissions.Reservations.OverridePrice, "Reservations", "Set a price different from the treatment type's price", IsSensitive: true),
        new(Permissions.Reservations.SetCompleted, "Reservations", "Set a visit to Completed outside the consultation screen", IsSensitive: true),
        new(Permissions.Reservations.ChangeAnyDoctorStatus, "Reservations", "Change the status of any doctor's reservation, not only their own"),

        new(Permissions.Records.View, "Medical records", "See medical records, prescriptions and their files; print prescriptions"),
        new(Permissions.Records.Write, "Medical records", "Write medical records, treatments and prescriptions; upload prescription files; complete consultations"),
        new(Permissions.Records.Lock, "Medical records", "Lock medical records", IsSensitive: true),
        new(Permissions.Consultations.ActOnBehalf, "Medical records", "Open, write and complete consultations for another doctor", IsSensitive: true),
        new(Permissions.Medicines.Search, "Medical records", "Search the medicines list"),

        new(Permissions.Analyses.View, "Lab", "See lab requests and results; print request slips"),
        new(Permissions.Analyses.Manage, "Lab", "Request analyses, update their status and upload results"),
        new(Permissions.Attachments.Delete, "Lab", "Delete analysis and prescription files", IsSensitive: true),

        new(Permissions.FollowUps.View, "Follow-ups", "See the follow-up list"),
        new(Permissions.FollowUps.Manage, "Follow-ups", "Book and dismiss follow-ups"),
        new(Permissions.FollowUps.ViewAll, "Follow-ups", "See and book follow-ups for every doctor"),

        new(Permissions.Nutrition.View, "Nutrition", "See packages, enrollments and weekly follow-ups"),
        new(Permissions.Nutrition.Manage, "Nutrition", "Enroll patients; record, edit and delete weekly follow-ups"),
        new(Permissions.Nutrition.ManagePackages, "Nutrition", "Create and deactivate nutrition packages"),

        new(Permissions.Payments.View, "Payments", "See patient payment summaries, the payment audit and unpaid visits"),
        new(Permissions.Payments.Collect, "Payments", "Collect payments"),
        new(Permissions.Payments.Adjust, "Payments", "Cancel payments and change amounts", IsSensitive: true),
        new(Permissions.Payments.ViewReport, "Payments", "Open the payment report"),
        new(Permissions.Payments.ReportAnyDate, "Payments", "Choose any dates in the payment report (others see today only)"),
        new(Permissions.Payments.ReportAllDoctors, "Payments", "See every doctor's payments in the report (others see their own)"),
        new(Permissions.Payments.ViewFullDashboard, "Payments", "See the full payment dashboard and its details"),
        new(Permissions.Payments.Maintenance, "Payments", "Run payment maintenance (recalculate statuses, backfill)", IsSensitive: true),

        new(Permissions.Settings.Clinics, "Administration", "Manage clinics and their agreements"),
        new(Permissions.Settings.PatientSources, "Administration", "Manage patient sources"),
        new(Permissions.Settings.Lookups, "Administration", "Manage analysis, treatment, injection and vitamin types"),
        new(Permissions.Users.Manage, "Administration", "Manage users, their roles and passwords", IsSensitive: true),
    };
}

public sealed record SystemGroupDefinition(string RoleName, string Name, string Description, IReadOnlyList<string> Permissions);

/// <summary>
/// F4a: one group per role in use, granting exactly what that role can do today (Program.cs
/// policies, [Authorize] attributes and the role checks in controllers). Until F4d retires
/// roles, a user belongs to a system group through the role named in <see cref="SystemGroupDefinition.RoleName"/>.
/// </summary>
public static class SystemGroups
{
    public const string AdminRole = "Admin";
    public const string DoctorRole = "Doctor";
    public const string NutritionistRole = "Nutritionist";
    public const string ReceptionRole = "Reception";
    public const string ReceptionAdminRole = "ReceptionAdmin";

    public static readonly IReadOnlyList<SystemGroupDefinition> All = new SystemGroupDefinition[]
    {
        new(AdminRole, "Administrators", "Full access, including users and settings",
            PermissionCatalogue.All.Select(p => p.Code).ToArray()),

        new(DoctorRole, "Doctors", "Consultations, medical records, lab requests and their own payment report", new[]
        {
            Permissions.Patients.View, Permissions.Patients.Edit, Permissions.Patients.ViewTimeline, Permissions.Patients.EditMedical,
            Permissions.Reservations.View, Permissions.Reservations.Manage, Permissions.Reservations.ViewQueue, Permissions.Reservations.StartConsultation,
            Permissions.Records.View, Permissions.Records.Write, Permissions.Medicines.Search,
            Permissions.Analyses.View, Permissions.Analyses.Manage,
            Permissions.FollowUps.View, Permissions.FollowUps.Manage,
            Permissions.Payments.ViewReport, Permissions.Payments.ReportAnyDate,
        }),

        new(NutritionistRole, "Nutritionists", "Nutrition enrollments and weekly follow-ups", new[]
        {
            Permissions.Patients.View, Permissions.Patients.Edit,
            Permissions.Reservations.View, Permissions.Reservations.Manage,
            Permissions.Medicines.Search,
            Permissions.Nutrition.View, Permissions.Nutrition.Manage,
        }),

        new(ReceptionRole, "Reception", "Front desk: patients, reservations, check-in, follow-ups and payments", new[]
        {
            Permissions.Patients.View, Permissions.Patients.Edit,
            Permissions.Reservations.View, Permissions.Reservations.Manage, Permissions.Reservations.ViewQueue,
            Permissions.Reservations.ViewAllQueues, Permissions.Reservations.CheckIn, Permissions.Reservations.ChangeAnyDoctorStatus,
            Permissions.Medicines.Search,
            Permissions.FollowUps.View, Permissions.FollowUps.Manage, Permissions.FollowUps.ViewAll,
            Permissions.Payments.View, Permissions.Payments.Collect, Permissions.Payments.Adjust,
            Permissions.Payments.ViewReport, Permissions.Payments.ReportAllDoctors,
        }),

        new(ReceptionAdminRole, "Reception supervisors", "Payments and payment reports", new[]
        {
            Permissions.Payments.View, Permissions.Payments.Collect, Permissions.Payments.Adjust,
            Permissions.Payments.ViewReport, Permissions.Payments.ReportAllDoctors,
        }),
    };
}
```

**4b.** Create `SafyaClinic/SafyaClinic.Application/Security/ClinicianTypes.cs`:

```csharp
using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.Security;

/// <summary>F4a: until roles are retired (F4d) the clinician type follows the roles; Doctor wins over Nutritionist.</summary>
public static class ClinicianTypes
{
    public static ClinicianType FromRoles(IEnumerable<string> roleNames)
    {
        var names = roleNames.ToList();
        if (names.Contains(SystemGroups.DoctorRole)) return ClinicianType.Doctor;
        if (names.Contains(SystemGroups.NutritionistRole)) return ClinicianType.Nutritionist;
        return ClinicianType.None;
    }
}
```

### Step 5 — `UserService`

File `SafyaClinic/SafyaClinic.Application/Services/UserService.cs`.

**5a.** Add with the other `using` lines:

```csharp
using SafyaClinic.Domain.Enums;
```

**5b.** In `CreateUserAsync`, right after the `foreach (var roleId in requestedRoleIds) { … }` validation loop (the one returning `"Role ID … does not exist."`), add:

```csharp

        // F4a: the clinician type follows the roles until roles are retired (F4d).
        var requestedRoleNames = await _uow.Roles
            .Query()
            .Where(role => requestedRoleIds.Contains(role.Id))
            .Select(role => role.RoleName)
            .ToListAsync();
```

and in the `new User { … }` block, after `LicenseNumber = request.LicenseNumber,` add:

```csharp
            ClinicianType = ClinicianTypes.FromRoles(requestedRoleNames),
```

**5c.** In `GetDoctorsAsync`, replace these lines:

```csharp
        // Doctor role = ID 2, Nutritionist = ID 5
        var doctors = await _uow.Users
            .Query()
            .Where(u => u.IsActive &&
                        u.UserRoles.Any(ur => ur.RoleId == 2 || ur.RoleId == 5))
```

with:

```csharp
        // F4a: clinicians are marked by ClinicianType (was role IDs 2 and 5).
        var doctors = await _uow.Users
            .Query()
            .Where(u => u.IsActive && u.ClinicianType != ClinicianType.None)
```

**5d.** In `AssignRoleAsync`, replace:

```csharp
        await RenewStampAsync(userId);   // the user signs in again and gets the new role
```

with:

```csharp
        var roleNames = await RoleNamesAsync(userId);
        roleNames.Add(await _uow.Roles.Query().Where(r => r.Id == roleId).Select(r => r.RoleName).FirstAsync());
        await RenewStampAsync(userId, ClinicianTypes.FromRoles(roleNames));   // the user signs in again and gets the new role
```

**5e.** In `RemoveRoleAsync`, replace:

```csharp
        await RenewStampAsync(userId);   // the removed role stops working at once
```

with:

```csharp
        var roleNames = await RoleNamesAsync(userId);
        roleNames.Remove(userRole.Role.RoleName);
        await RenewStampAsync(userId, ClinicianTypes.FromRoles(roleNames));   // the removed role stops working at once
```

**5f.** Replace the whole `RenewStampAsync` method with these two methods:

```csharp
    private async Task RenewStampAsync(int userId, ClinicianType clinicianType)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null) return;

        user.SecurityStamp = SecurityStamps.New();
        user.ClinicianType = clinicianType;   // F4a: follows the roles until F4d
        _uow.Users.Update(user);
    }

    // Saved roles only: the caller adds or removes the role it is changing.
    private Task<List<string>> RoleNamesAsync(int userId) =>
        _uow.UserRoles.Query()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.RoleName)
            .ToListAsync();
```

### Step 6 — Infrastructure

**6a.** Create `SafyaClinic/SafyaClinic.Infrastructure/Data/Configurations/PermissionGroupConfigurations.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    // F4a (D1): permissions, groups, grants and memberships.

    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("Permissions");
            builder.HasKey(p => p.Code);
            builder.Property(p => p.Code).HasMaxLength(100);
            builder.Property(p => p.Area).IsRequired().HasMaxLength(50);
            builder.Property(p => p.Description).IsRequired().HasMaxLength(200);
        }
    }

    public class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
    {
        public void Configure(EntityTypeBuilder<PermissionGroup> builder)
        {
            builder.ToTable("PermissionGroups");
            builder.HasKey(g => g.Id);
            builder.Property(g => g.Name).IsRequired().HasMaxLength(100);
            builder.Property(g => g.Description).HasMaxLength(300);
            builder.Property(g => g.SystemKey).HasMaxLength(50);
            builder.HasIndex(g => g.Name).IsUnique();
            builder.HasIndex(g => g.SystemKey).IsUnique();   // SQL Server filters it to non-null values
        }
    }

    public class GroupPermissionConfiguration : IEntityTypeConfiguration<GroupPermission>
    {
        public void Configure(EntityTypeBuilder<GroupPermission> builder)
        {
            builder.ToTable("GroupPermissions");
            builder.HasKey(gp => new { gp.GroupId, gp.PermissionCode });
            builder.Property(gp => gp.PermissionCode).HasMaxLength(100);

            builder.HasOne(gp => gp.Group)
                   .WithMany(g => g.GroupPermissions)
                   .HasForeignKey(gp => gp.GroupId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(gp => gp.Permission)
                   .WithMany(p => p.GroupPermissions)
                   .HasForeignKey(gp => gp.PermissionCode)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
    {
        public void Configure(EntityTypeBuilder<UserGroup> builder)
        {
            builder.ToTable("UserGroups");
            builder.HasKey(ug => new { ug.UserId, ug.GroupId });

            builder.HasOne(ug => ug.User)
                   .WithMany(u => u.UserGroups)
                   .HasForeignKey(ug => ug.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ug => ug.Group)
                   .WithMany(g => g.Members)
                   .HasForeignKey(ug => ug.GroupId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

**6b.** `UserConfiguration.cs`: after the `builder.Property(u => u.LicenseNumber)…` line, add:

```csharp
            builder.Property(u => u.ClinicianType).HasConversion<string>().HasMaxLength(20);   // F4a
```

**6c.** `SafyaDbContext.cs`: after `public DbSet<UserRole> UserRoles { get; set; }`, add:

```csharp
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<PermissionGroup> PermissionGroups { get; set; }
        public DbSet<GroupPermission> GroupPermissions { get; set; }
        public DbSet<UserGroup> UserGroups { get; set; }
```

**6d.** `DbSeeder.cs`:

- In `SeedAsync`, after `await EnsureReceptionAdminRoleAsync(context);`, add:

  ```csharp
          await SeedPermissionsAsync(context);
  ```

- Add this method right after the `EnsureReceptionAdminRoleAsync` method (before `public const string AdminPasswordSettingKey`):

```csharp

    // ── Permissions and system groups (F4a) ─────────────────
    /// <summary>
    /// Copies the permission catalogue from code and creates one group per role in use, granting
    /// what that role can do today. Each group is created once; grants an admin removes later stay
    /// removed, except that Administrators always receive every permission.
    /// </summary>
    private static async Task SeedPermissionsAsync(SafyaDbContext context)
    {
        var existing = await context.Permissions.ToDictionaryAsync(p => p.Code);
        foreach (var definition in PermissionCatalogue.All)
        {
            if (!existing.TryGetValue(definition.Code, out var permission))
            {
                permission = new Permission { Code = definition.Code };
                context.Permissions.Add(permission);
            }
            permission.Area = definition.Area;
            permission.Description = definition.Description;
            permission.IsSensitive = definition.IsSensitive;
        }
        await context.SaveChangesAsync();

        foreach (var definition in SystemGroups.All)
        {
            var group = await context.PermissionGroups
                .Include(g => g.GroupPermissions)
                .FirstOrDefaultAsync(g => g.SystemKey == definition.RoleName);

            if (group is null)
            {
                group = new PermissionGroup
                {
                    Name = definition.Name,
                    Description = definition.Description,
                    SystemKey = definition.RoleName
                };
                context.PermissionGroups.Add(group);
            }
            else if (definition.RoleName != SystemGroups.AdminRole)
            {
                continue;   // an existing group keeps the grants an admin gave it
            }

            var granted = group.GroupPermissions.Select(p => p.PermissionCode).ToHashSet();
            foreach (var code in definition.Permissions.Where(c => !granted.Contains(c)))
                group.GroupPermissions.Add(new GroupPermission { PermissionCode = code });
        }
        await context.SaveChangesAsync();
    }
```

**Check:** `dotnet build SafyaClinic\SafyaClinic.sln` gives 0 errors. The 4 old warnings may remain.

### Step 7 — Tests

**7a.** Create `SafyaClinic/tests/SafyaClinic.Tests/Application/PermissionCatalogueTests.cs`:

```csharp
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Enums;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F4a: the catalogue is consistent and the system groups grant exactly today's access.</summary>
public class PermissionCatalogueTests
{
    // Today's access per permission, read from the Program.cs policies, the [Authorize] attributes and
    // the role checks in controllers (F4a guide, appendix). A = Admin, D = Doctor, N = Nutritionist,
    // R = Reception, RA = ReceptionAdmin.
    private static readonly Dictionary<string, string> TodaysAccess = new()
    {
        [Permissions.Patients.View] = "A D N R",
        [Permissions.Patients.Edit] = "A D N R",
        [Permissions.Patients.ViewTimeline] = "A D",
        [Permissions.Patients.EditMedical] = "A D",
        [Permissions.Reservations.View] = "A D N R",
        [Permissions.Reservations.Manage] = "A D N R",
        [Permissions.Reservations.ViewQueue] = "A D R",
        [Permissions.Reservations.ViewAllQueues] = "A R",
        [Permissions.Reservations.CheckIn] = "A R",
        [Permissions.Reservations.StartConsultation] = "A D",
        [Permissions.Reservations.OverridePrice] = "A",
        [Permissions.Reservations.SetCompleted] = "A",
        [Permissions.Reservations.ChangeAnyDoctorStatus] = "A R",
        [Permissions.Records.View] = "A D",
        [Permissions.Records.Write] = "A D",
        [Permissions.Records.Lock] = "A",
        [Permissions.Consultations.ActOnBehalf] = "A",
        [Permissions.Medicines.Search] = "A D N R",
        [Permissions.Analyses.View] = "A D",
        [Permissions.Analyses.Manage] = "A D",
        [Permissions.Attachments.Delete] = "A",
        [Permissions.FollowUps.View] = "A D R",
        [Permissions.FollowUps.Manage] = "A D R",
        [Permissions.FollowUps.ViewAll] = "A R",
        [Permissions.Nutrition.View] = "A N",
        [Permissions.Nutrition.Manage] = "A N",
        [Permissions.Nutrition.ManagePackages] = "A",
        [Permissions.Payments.View] = "A R RA",
        [Permissions.Payments.Collect] = "A R RA",
        [Permissions.Payments.Adjust] = "A R RA",
        [Permissions.Payments.ViewReport] = "A D R RA",
        [Permissions.Payments.ReportAnyDate] = "A D",
        [Permissions.Payments.ReportAllDoctors] = "A R RA",
        [Permissions.Payments.ViewFullDashboard] = "A",
        [Permissions.Payments.Maintenance] = "A",
        [Permissions.Settings.Clinics] = "A",
        [Permissions.Settings.PatientSources] = "A",
        [Permissions.Settings.Lookups] = "A",
        [Permissions.Users.Manage] = "A",
    };

    private static readonly Dictionary<string, string> RoleOfLetter = new()
    {
        ["A"] = SystemGroups.AdminRole,
        ["D"] = SystemGroups.DoctorRole,
        ["N"] = SystemGroups.NutritionistRole,
        ["R"] = SystemGroups.ReceptionRole,
        ["RA"] = SystemGroups.ReceptionAdminRole,
    };

    private static IEnumerable<string> AllCodes => PermissionCatalogue.All.Select(p => p.Code).OrderBy(c => c);

    [Fact]
    public void Codes_are_unique_and_described()
    {
        Assert.Equal(PermissionCatalogue.All.Count, AllCodes.Distinct().Count());
        Assert.All(PermissionCatalogue.All, p =>
        {
            Assert.Matches(@"^[A-Z][A-Za-z]+\.[A-Z][A-Za-z]+$", p.Code);
            Assert.False(string.IsNullOrWhiteSpace(p.Area));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
        });
    }

    [Fact]
    public void System_groups_only_grant_known_permissions_once()
    {
        var known = AllCodes.ToHashSet();
        Assert.All(SystemGroups.All, g =>
        {
            Assert.All(g.Permissions, code => Assert.Contains(code, known));
            Assert.Equal(g.Permissions.Count, g.Permissions.Distinct().Count());
        });
    }

    [Fact]
    public void Administrators_have_every_permission()
    {
        var admin = SystemGroups.All.Single(g => g.RoleName == SystemGroups.AdminRole);
        Assert.Equal(AllCodes, admin.Permissions.OrderBy(c => c));
    }

    [Fact]
    public void The_access_table_covers_every_permission()
    {
        Assert.Equal(AllCodes, TodaysAccess.Keys.OrderBy(c => c));
    }

    [Fact]
    public void System_groups_grant_exactly_todays_access()
    {
        var mismatches = new List<string>();
        foreach (var (code, letters) in TodaysAccess)
        {
            var expected = letters.Split(' ').Select(l => RoleOfLetter[l]).OrderBy(r => r).ToList();
            var actual = SystemGroups.All.Where(g => g.Permissions.Contains(code)).Select(g => g.RoleName).OrderBy(r => r).ToList();
            if (!expected.SequenceEqual(actual))
                mismatches.Add($"{code}: expected {string.Join(",", expected)} but got {string.Join(",", actual)}");
        }

        Assert.Empty(mismatches);
    }

    [Theory]
    [InlineData("Doctor", ClinicianType.Doctor)]
    [InlineData("Nutritionist", ClinicianType.Nutritionist)]
    [InlineData("Nutritionist,Doctor", ClinicianType.Doctor)]
    [InlineData("Admin,Reception", ClinicianType.None)]
    public void Clinician_type_follows_the_roles(string roles, ClinicianType expected)
    {
        Assert.Equal(expected, ClinicianTypes.FromRoles(roles.Split(',')));
    }
}
```

**7b.** Create `SafyaClinic/tests/SafyaClinic.Tests/Application/ClinicianTypeTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F4a: doctor pickers use ClinicianType, which follows the Doctor and Nutritionist roles.</summary>
public class ClinicianTypeTests
{
    private static async Task<(SafyaDbContext Context, int AdminId)> SeedAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"clinicians-{Guid.NewGuid()}")
            .Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Clinician-Test-Password-1" })
            .Build());
        var adminId = await context.Users.Select(u => u.Id).SingleAsync();
        context.ChangeTracker.Clear();
        return (context, adminId);
    }

    private static UserService Service(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new UserService(new UnitOfWork(context));
    }

    private static Task<int> RoleId(SafyaDbContext context, string name) =>
        context.Roles.Where(r => r.RoleName == name).Select(r => r.Id).SingleAsync();

    private static async Task<int> AddUserAsync(SafyaDbContext context, string phone,
        ClinicianType type = ClinicianType.None, bool active = true)
    {
        var user = new User
        {
            FullName = "User " + phone, PhoneNumber = phone, PasswordHash = "x",
            SecurityStamp = "stamp", ClinicianType = type, IsActive = active
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return user.Id;
    }

    private static Task<ClinicianType> TypeOf(SafyaDbContext context, int userId) =>
        context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.ClinicianType).SingleAsync();

    [Fact]
    public async Task Assigning_and_removing_the_doctor_role_updates_the_clinician_type()
    {
        var (context, adminId) = await SeedAsync();
        var userId = await AddUserAsync(context, "0155");
        var doctorRoleId = await RoleId(context, "Doctor");

        Assert.True((await Service(context).AssignRoleAsync(userId, doctorRoleId, adminId)).IsSuccess);
        Assert.Equal(ClinicianType.Doctor, await TypeOf(context, userId));

        Assert.True((await Service(context).RemoveRoleAsync(userId, doctorRoleId)).IsSuccess);
        Assert.Equal(ClinicianType.None, await TypeOf(context, userId));
    }

    [Fact]
    public async Task Creating_a_nutritionist_sets_the_clinician_type()
    {
        var (context, adminId) = await SeedAsync();
        var request = new CreateUserRequest
        {
            FullName = "Nutritionist",
            PhoneNumber = "0166",
            Password = "Create-Test-Password-1",
            ConfirmPassword = "Create-Test-Password-1",
            RoleIds = new() { await RoleId(context, "Nutritionist") }
        };

        var result = await Service(context).CreateUserAsync(request, adminId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ClinicianType.Nutritionist, await TypeOf(context, result.Data!.Id));
    }

    [Fact]
    public async Task Doctor_list_contains_only_active_clinicians()
    {
        var (context, _) = await SeedAsync();
        var doctorId = await AddUserAsync(context, "0177", ClinicianType.Doctor);
        var nutritionistId = await AddUserAsync(context, "0188", ClinicianType.Nutritionist);
        await AddUserAsync(context, "0199", ClinicianType.Doctor, active: false);
        await AddUserAsync(context, "0111");

        var result = await Service(context).GetDoctorsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { doctorId, nutritionistId }, result.Data!.Select(d => d.Id).OrderBy(id => id));
    }
}
```

**7c.** Create `SafyaClinic/tests/SafyaClinic.Tests/Infrastructure/PermissionSeedingTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using Xunit;

namespace SafyaClinic.Tests.Infrastructure;

/// <summary>F4a: the seeder copies the catalogue and creates one group per role, once.</summary>
public class PermissionSeedingTests
{
    private static SafyaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"permissions-{Guid.NewGuid()}")
            .Options);

    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Seed-Test-Password-1" })
        .Build();

    private static Task<int> GroupId(SafyaDbContext context, string roleName) =>
        context.PermissionGroups.Where(g => g.SystemKey == roleName).Select(g => g.Id).SingleAsync();

    [Fact]
    public async Task Seeder_copies_the_catalogue_and_creates_the_system_groups()
    {
        await using var context = NewContext();

        await DbSeeder.SeedAsync(context, Config);

        Assert.Equal(PermissionCatalogue.All.Count, await context.Permissions.CountAsync());
        Assert.Equal(SystemGroups.All.Count, await context.PermissionGroups.CountAsync());
        foreach (var definition in SystemGroups.All)
        {
            var group = await context.PermissionGroups
                .Include(g => g.GroupPermissions)
                .SingleAsync(g => g.SystemKey == definition.RoleName);
            Assert.Equal(definition.Name, group.Name);
            Assert.Equal(definition.Permissions.OrderBy(c => c),
                group.GroupPermissions.Select(p => p.PermissionCode).OrderBy(c => c));
        }
    }

    [Fact]
    public async Task Seeding_again_adds_nothing()
    {
        await using var context = NewContext();
        await DbSeeder.SeedAsync(context, Config);
        var permissions = await context.Permissions.CountAsync();
        var groups = await context.PermissionGroups.CountAsync();
        var grants = await context.GroupPermissions.CountAsync();
        context.ChangeTracker.Clear();

        await DbSeeder.SeedAsync(context, Config);

        Assert.Equal(permissions, await context.Permissions.CountAsync());
        Assert.Equal(groups, await context.PermissionGroups.CountAsync());
        Assert.Equal(grants, await context.GroupPermissions.CountAsync());
    }

    [Fact]
    public async Task Removed_grants_stay_removed_except_for_administrators()
    {
        await using var context = NewContext();
        await DbSeeder.SeedAsync(context, Config);
        var doctorsId = await GroupId(context, SystemGroups.DoctorRole);
        var adminsId = await GroupId(context, SystemGroups.AdminRole);
        context.GroupPermissions.RemoveRange(await context.GroupPermissions
            .Where(p => (p.GroupId == doctorsId || p.GroupId == adminsId) && p.PermissionCode == Permissions.Patients.EditMedical)
            .ToListAsync());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await DbSeeder.SeedAsync(context, Config);

        Assert.False(await context.GroupPermissions.AnyAsync(p => p.GroupId == doctorsId && p.PermissionCode == Permissions.Patients.EditMedical));
        Assert.True(await context.GroupPermissions.AnyAsync(p => p.GroupId == adminsId && p.PermissionCode == Permissions.Patients.EditMedical));
    }

    [Fact]
    public void Clinician_type_is_stored_as_text()
    {
        var model = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseSqlServer("Server=model-only;Database=ModelOnly;Integrated Security=True").Options).Model;

        var property = model.FindEntityType(typeof(User))!.FindProperty(nameof(User.ClinicianType))!;

        Assert.Equal(typeof(string), property.GetProviderClrType());
        Assert.Equal(20, property.GetMaxLength());
    }
}
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: **Passed: 166** (150 + 16), Failed: 0. If anything fails, send me the output. Don't change the tests to make them pass.

### Step 8 — Migration (needs your approval)

**8a.** Generate:

```powershell
Push-Location SafyaClinic\SafyaClinic.Web
dotnet ef migrations add PermissionGroups --project ..\SafyaClinic.Infrastructure
Pop-Location
```

`Up` must contain only:

- one `AddColumn` named `ClinicianType` on `Users` (`nvarchar(20)`);
- four `CreateTable` calls: `Permissions`, `PermissionGroups`, `GroupPermissions`, `UserGroups`;
- `CreateIndex` calls for those tables: `IX_GroupPermissions_PermissionCode`, `IX_PermissionGroups_Name` (unique), `IX_PermissionGroups_SystemKey` (unique, with `filter: "[SystemKey] IS NOT NULL"`), `IX_UserGroups_GroupId`.

If it contains anything else, stop and send me the file.

**8b.** In the `AddColumn` for `ClinicianType`, change `defaultValue: ""` to `defaultValue: "None"`. Then, **directly after** that `AddColumn(…);` statement, add:

```csharp

            // F4a: clinician type from today's roles, by role name (not ID). Doctor wins over Nutritionist.
            migrationBuilder.Sql(@"
UPDATE u SET ClinicianType =
    CASE WHEN EXISTS (SELECT 1 FROM UserRoles ur JOIN Roles r ON r.Id = ur.RoleId
                      WHERE ur.UserId = u.Id AND r.RoleName = N'Doctor') THEN N'Doctor'
         WHEN EXISTS (SELECT 1 FROM UserRoles ur JOIN Roles r ON r.Id = ur.RoleId
                      WHERE ur.UserId = u.Id AND r.RoleName = N'Nutritionist') THEN N'Nutritionist'
         ELSE N'None' END
FROM Users u;");
```

Leave `Down` as generated: it drops the tables and the column.

**8c.** Check that the model and migrations agree:

```powershell
dotnet run --project SafyaClinic\tests\DeploymentChecks -- D:\MyDevWork\migrations-idempotent.sql
```

It must print `Model changes without a migration: False`.

**8d.** Back up and start the app:

```powershell
powershell -ExecutionPolicy Bypass -File .assessment\backup-db.ps1
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

Running 8d is your explicit approval for these changes to the **dev** database: four new tables, the new `ClinicianType` column filled from roles, and the seeder adding 39 permissions and 5 groups. If the migration fails, nothing changes; send me the error.

### Step 9 — Checks

**9a.** Database (read-only). Run in a new PowerShell window from `D:\MyDevWork\clinic-MS`:

```powershell
$cfg = Get-Content SafyaClinic\SafyaClinic.Web\appsettings.json -Raw | ConvertFrom-Json
$conn = New-Object System.Data.SqlClient.SqlConnection $cfg.ConnectionStrings.SafyaClinicDb
$conn.Open()
function Show($sql) { $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $t = New-Object System.Data.DataTable; $t.Load($cmd.ExecuteReader()); $t | Format-Table -AutoSize }
Show "SELECT g.Name, g.SystemKey, COUNT(gp.PermissionCode) AS Permissions FROM PermissionGroups g LEFT JOIN GroupPermissions gp ON gp.GroupId = g.Id GROUP BY g.Name, g.SystemKey ORDER BY g.Name"
Show "SELECT ClinicianType, COUNT(*) AS Users FROM Users GROUP BY ClinicianType"
Show "SELECT (SELECT COUNT(*) FROM Users WHERE IsActive = 1 AND ClinicianType <> 'None') AS NewDoctorList, (SELECT COUNT(DISTINCT u.Id) FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id WHERE u.IsActive = 1 AND ur.RoleId IN (2, 5)) AS OldDoctorList"
$conn.Close()
```

Expected:
- Administrators 39, Doctors 17, Nutritionists 7, Reception 17, Reception supervisors 5.
- The ClinicianType counts match the Doctor and Nutritionist users in Step 1.
- `NewDoctorList` = `OldDoctorList`.

**9b.** In the app: open **Reservations → New**. The doctor list shows the same people as before. Then on **Users**, give a test user the Nutritionist role. They now appear in that list. Remove the role, and they disappear.

### Step 10 — Commit, push, PR

```powershell
git add SafyaClinic/SafyaClinic.Domain SafyaClinic/SafyaClinic.Application SafyaClinic/SafyaClinic.Infrastructure SafyaClinic/tests Docs
git status
git commit -m "F4a: permission catalogue, permission group tables, system groups mirroring roles, ClinicianType replaces role IDs"
git push -u origin chore/f4a-permission-groups
```

`git status` also shows `Docs/deployment/D1-deploy-group1-and-foundations.md`; it belongs in this commit. Open the PR and send me the number, the CI result and the Step 1 and 9a output. Don't merge before review.

## Hosted servers

Nothing extra. The fresh deployment before the pilot creates these tables with the full migration script, and the seeder adds the catalogue and groups.

## Acceptance criteria

- [ ] `Permissions` has 39 rows; five system groups exist with 39 / 17 / 7 / 17 / 5 grants.
- [ ] `Users.ClinicianType` is filled from roles; the doctor list is unchanged (9a, 9b).
- [ ] Assigning or removing the Doctor or Nutritionist role updates the clinician type.
- [ ] No code uses role IDs 2 and 5 any more.
- [ ] Nothing a user can do has changed.
- [ ] 166 tests pass locally and in CI.

---

## Appendix — Where each permission comes from

Controller rules **combine**: an action's own policy applies on top of the controller's. For example, `Patients/Timeline` needs `ClinicalStaff` **and** `DoctorOrAdmin`, which means Admin or Doctor. F4b replaces each attribute or check below with the permission on the left.

| Permission | Today | Used by |
|---|---|---|
| Patients.View | A D N R | Patients `Index`, `Details`; `api/patients/search` |
| Patients.Edit | A D N R | Patients `Create`, `EditBasic`, `AddPhone`, `RemovePhone`, `AddAddress`, `RemoveAddress` |
| Patients.ViewTimeline | A D | Patients `Timeline` |
| Patients.EditMedical | A D | Patients `EditMedical` |
| Reservations.View | A D N R | Reservations `Index`, `Today`, `Board`, `Details`, `PatientReservationHistoryReport` |
| Reservations.Manage | A D N R | Reservations `Create`, `Edit`, `UpdateStatus`, `Cancel` |
| Reservations.ViewQueue | A D R | Reservations `Queue` |
| Reservations.ViewAllQueues | A R | `canViewAllQueues` in Reservations `Queue` |
| Reservations.CheckIn | A R | Reservations `CheckIn` |
| Reservations.StartConsultation | A D | Reservations `StartConsultation` |
| Reservations.OverridePrice | A | `ReservationRules.CanOverridePrice` (D4) |
| Reservations.SetCompleted | A | `ReservationRules.CheckStatusChange`, Completed (D4) |
| Reservations.ChangeAnyDoctorStatus | A R | `ReservationRules.CheckStatusChange`, other doctor's reservation (D6) |
| Records.View | A D | MedicalRecords `PatientRecords`, `Details`, `PrescriptionDetails`, `ViewAttachment`, `DownloadAttachment`, `PrintPrescription`, `Consultation` |
| Records.Write | A D | MedicalRecords `Create`, `Edit`, `AddTreatment`, `RemoveTreatment`, `CreatePrescription`, `AddPrescriptionItem`, `RemovePrescriptionItem`, `MarkPrescriptionPrinted`, `UploadPrescriptionAttachment`, `CompleteConsultation` |
| Records.Lock | A | MedicalRecords `Lock` |
| Consultations.ActOnBehalf | A | `IsAdmin` passed to `GetConsultationContextAsync`, `CreateRecordAsync`, `CompleteConsultationAsync`, `StartConsultationAsync` |
| Medicines.Search | A D N R | `api/medicines/search` |
| Analyses.View | A D | Analysis `Index`, `PatientAnalyses`, `Details`, `PrintRequestSlip`, `PrintPatientRequests`, `ViewAttachment`, `DownloadAttachment` |
| Analyses.Manage | A D | Analysis `GetRequest`, `PostRequest`, `UpdateStatus`, `UploadAttachment` |
| Attachments.Delete | A | Analysis `DeleteAttachment`, MedicalRecords `DeleteAttachment` |
| FollowUps.View | A D R | FollowUps `Index` |
| FollowUps.Manage | A D R | FollowUps `Book`, `Dismiss` |
| FollowUps.ViewAll | A R | `IsAdmin \|\| IsReception` in FollowUps |
| Nutrition.View | A N | Nutrition `Packages`, `PackageDetails`, `PatientEnrollments`, `EnrollmentDetails`, `FollowUpDetails` |
| Nutrition.Manage | A N | Nutrition `Enroll`, `UpdateEnrollmentStatus`, `RecordFollowUp`, `CompleteFollowUp`, `EditFollowUp`, `DeleteFollowUp` |
| Nutrition.ManagePackages | A | Nutrition `CreatePackage`, `DeactivatePackage` |
| Payments.View | A R RA | Payments `PatientSummary`, `Audit`, `Dashboard` |
| Payments.Collect | A R RA | Payments `Collect`. Also the "amount due / collect" prompts (`IsAdmin \|\| IsReception`) in Reservations `Details`, `UpdateStatus` and MedicalRecords `CompleteConsultation` |
| Payments.Adjust | A R RA | Payments `Cancel`, `ChangeAmount` |
| Payments.ViewReport | A D R RA | Payments `Report` |
| Payments.ReportAnyDate | A D | `todayOnly = !IsAdmin && !IsDoctor` in Payments `Report` |
| Payments.ReportAllDoctors | A R RA | doctor filter `!IsAdmin && IsDoctor` in Payments `Report` |
| Payments.ViewFullDashboard | A | `IsAdmin` in Payments `Dashboard`; `DashboardLineDetails` |
| Payments.Maintenance | A | Payments `RecalculateAllPaidStatuses`, `BackfillZeroCostPayments` |
| Settings.Clinics | A | Clinics (all) |
| Settings.PatientSources | A | PatientSources (all) |
| Settings.Lookups | A | Lookups (all); Nutrition `InjectionTypes`, `VitaminTypes` and their create, edit and delete actions |
| Users.Manage | A | Users (all), Roles (all) |

Not permissions: Dashboard (`[Authorize]`, every signed-in user) and `IsDoctor \|\| IsNutritionist` in Dashboard, which shows a clinician their own day. In F4b that check becomes `ClinicianType != None`.
