namespace SafyaClinic.Application.Security;

/// <summary>F4b: reads a user's effective permissions from the database.</summary>
public interface IPermissionStore
{
    /// <summary>
    /// The grants of every system group whose role the user has, plus groups the user is a
    /// direct member of. Empty for unknown or inactive users.
    /// </summary>
    Task<IReadOnlySet<string>> GetPermissionsAsync(int userId, CancellationToken cancellationToken = default);
}