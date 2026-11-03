using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Tests;

public static class TestData
{
    /// <summary>F4d: puts a user in a built-in group, named by its former role (Admin, Doctor, Nutritionist, Reception, ReceptionAdmin).</summary>
    public static async Task AddToBuiltInGroupAsync(SafyaDbContext context, int userId, string systemKey)
    {
        var groupId = await context.PermissionGroups
            .Where(g => g.SystemKey == systemKey)
            .Select(g => g.Id)
            .SingleAsync();
        context.UserGroups.Add(new UserGroup { UserId = userId, GroupId = groupId });
        await context.SaveChangesAsync();
    }
}
