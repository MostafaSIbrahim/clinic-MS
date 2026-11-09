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
        public const string WriteOff = "Payments.WriteOff";   // P6b
    }

    public static class Settings
    {
        public const string Clinics = "Settings.Clinics";
        public const string PatientSources = "Settings.PatientSources";
        public const string Lookups = "Settings.Lookups";
    }

    public static class Schedule
    {
        public const string ManageOwn = "Schedule.ManageOwn";
        public const string ManageAll = "Schedule.ManageAll";
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
        new(Permissions.Payments.WriteOff, "Payments", "Write off a visit's balance, and undo write-offs (with a reason; audited)", IsSensitive: true),

        new(Permissions.Schedule.ManageOwn, "Scheduling", "Set their own calendar: mode, step, working periods, breaks and slots"),
        new(Permissions.Schedule.ManageAll, "Scheduling", "Set any doctor's calendar"),

        new(Permissions.Settings.Clinics, "Administration", "Manage clinics and their agreements"),
        new(Permissions.Settings.PatientSources, "Administration", "Manage patient sources"),
        new(Permissions.Settings.Lookups, "Administration", "Manage analysis, treatment, injection and vitamin types"),
        new(Permissions.Users.Manage, "Administration", "Manage users, their roles and passwords", IsSensitive: true),
    };
}

public sealed record SystemGroupDefinition(string RoleName, string Name, string Description, IReadOnlyList<string> Permissions);

/// <summary>
/// F4a: the built-in groups, one per former role, granting what that role could do. Roles are gone (F4d);
/// <see cref="SystemGroupDefinition.RoleName"/> survives only as each group's fixed <c>SystemKey</c>.
/// </summary>
public static class SystemGroups
{
    public const string AdminRole = "Admin";
    public const string DoctorRole = "Doctor";
    public const string NutritionistRole = "Nutritionist";
    public const string ReceptionRole = "Reception";
    public const string ReceptionAdminRole = "ReceptionAdmin";
    public const string ClinicAdminRole = "ClinicAdmin";   // P6b

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
            Permissions.Schedule.ManageOwn,
        }),

        new(NutritionistRole, "Nutritionists", "Nutrition enrollments and weekly follow-ups", new[]
        {
            Permissions.Patients.View, Permissions.Patients.Edit,
            Permissions.Reservations.View, Permissions.Reservations.Manage,
            Permissions.Medicines.Search,
            Permissions.Nutrition.View, Permissions.Nutrition.Manage,
            Permissions.Schedule.ManageOwn,
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

        new(ReceptionAdminRole, "Reception supervisors", "Payments, payment reports, write-offs and doctors' calendars", new[]
        {
            Permissions.Payments.View, Permissions.Payments.Collect, Permissions.Payments.Adjust,
            Permissions.Payments.ViewReport, Permissions.Payments.ReportAllDoctors,
            Permissions.Payments.WriteOff,
            Permissions.Schedule.ManageAll,
        }),

        // P6b: the clinic manager. Reception supervisors' access plus write-offs, the full payment dashboard,
        // reports for any date and clinic settings (including opening hours). No users, permissions or records.
        new(ClinicAdminRole, "Clinic managers", "Everything Reception supervisors do, plus the full payment dashboard, any-date reports and clinic settings", new[]
        {
            Permissions.Payments.View, Permissions.Payments.Collect, Permissions.Payments.Adjust,
            Permissions.Payments.ViewReport, Permissions.Payments.ReportAllDoctors,
            Permissions.Payments.WriteOff, Permissions.Payments.ViewFullDashboard, Permissions.Payments.ReportAnyDate,
            Permissions.Schedule.ManageAll,
            Permissions.Settings.Clinics,
        }),
    };
}