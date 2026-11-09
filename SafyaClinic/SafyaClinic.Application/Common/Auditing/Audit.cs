using System.Text.Json;
using SafyaClinic.Domain.Entities.Audit;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Common.Auditing;

/// <summary>Action names written to AuditLogs.Action (F3b).</summary>
public static class AuditActions
{
    public const string SignInSucceeded = "SignIn.Succeeded";
    public const string SignInFailed = "SignIn.Failed";
    public const string SignInLockedOut = "SignIn.LockedOut";
    public const string SignInRefusedWhileLocked = "SignIn.RefusedWhileLocked";
    public const string PasswordChanged = "User.PasswordChanged";
    public const string PasswordReset = "User.PasswordReset";
    public const string UserCreated = "User.Created";
    public const string UserActivated = "User.Activated";
    public const string UserDeactivated = "User.Deactivated";
    public const string RoleAssigned = "User.RoleAssigned";
    public const string RoleRemoved = "User.RoleRemoved";
    public const string ClinicianTypeChanged = "User.ClinicianTypeChanged";
    public const string RecordEdited = "MedicalRecord.Edited";
    public const string RecordLocked = "MedicalRecord.Locked";
    public const string PaymentCollected = "Payment.Collected";
    public const string PaymentCancelled = "Payment.Cancelled";
    public const string PaymentAmountChanged = "Payment.AmountChanged";
    public const string PaymentWrittenOff = "Payment.WrittenOff";                 // P6b
    public const string PaymentWriteOffCancelled = "Payment.WriteOffCancelled";   // P6b
    public const string GroupCreated = "Group.Created";
    public const string GroupUpdated = "Group.Updated";
    public const string GroupDeleted = "Group.Deleted";
    public const string GroupMemberAdded = "Group.MemberAdded";
    public const string GroupMemberRemoved = "Group.MemberRemoved";
    public const string ClinicHoursChanged = "Clinic.HoursChanged";   // P1
    public const string ScheduleChanged = "Schedule.Changed";         // P1
    public const string ScheduleDateChanged = "Schedule.DateChanged"; // P1c
    public const string ReservationMoved = "Reservation.Moved";       // P2c
    public const string AllergyOverride = "Prescription.AllergyOverride"; // P5a
    public const string RecordVisitLinked = "MedicalRecord.VisitLinked";  // P5c: a visit continues an existing record
}

/// <summary>Who is acting and from where — supplied by the web layer.</summary>
public interface IAuditContext
{
    int? UserId { get; }
    string? IpAddress { get; }
    string? RequestId { get; }
}

public interface IAuditTrail
{
    /// <summary>
    /// Adds an audit entry to the current unit of work. It is saved by the caller's next
    /// SaveChangesAsync, together with the change it describes.
    /// </summary>
    Task RecordAsync(string action, string entityType, int? entityId, object? details = null, int? actingUserId = null);
}

public sealed class AuditTrail : IAuditTrail
{
    private const int MaxDetailsLength = 4000;

    private readonly IUnitOfWork _uow;
    private readonly IAuditContext _context;

    public AuditTrail(IUnitOfWork uow, IAuditContext context)
    {
        _uow = uow;
        _context = context;
    }

    public Task RecordAsync(string action, string entityType, int? entityId, object? details = null, int? actingUserId = null)
    {
        var json = details is null ? null : JsonSerializer.Serialize(details);
        if (json is { Length: > MaxDetailsLength }) json = json[..MaxDetailsLength];

        return _uow.AuditLogs.AddAsync(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            UserId = actingUserId ?? _context.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = json,
            IpAddress = _context.IpAddress,
            RequestId = _context.RequestId
        });
    }
}

/// <summary>Used when no audit trail is supplied (some tests).</summary>
public sealed class NoAuditTrail : IAuditTrail
{
    public static readonly NoAuditTrail Instance = new();

    public Task RecordAsync(string action, string entityType, int? entityId, object? details = null, int? actingUserId = null) =>
        Task.CompletedTask;
}