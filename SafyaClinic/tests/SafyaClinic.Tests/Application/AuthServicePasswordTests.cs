using SafyaClinic.Application.Services;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// Characterization tests for the CURRENT password helpers. They document
/// today's behaviour so slice S5 can replace the hashing safely; S5 will
/// update these tests together with the new hasher.
/// </summary>
public class AuthServicePasswordTests
{
    private const string Password = "Correct-horse-1";

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = AuthService.HashPassword(Password);

        Assert.True(AuthService.VerifyPassword(Password, hash));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        var hash = AuthService.HashPassword(Password);

        Assert.False(AuthService.VerifyPassword("wrong-password", hash));
    }

    [Fact]
    public void Malformed_hash_fails_without_throwing()
    {
        Assert.False(AuthService.VerifyPassword(Password, "not-a-valid-hash"));
    }

    [Fact]
    public void Same_password_gets_a_different_salt_each_time()
    {
        Assert.NotEqual(AuthService.HashPassword(Password), AuthService.HashPassword(Password));
    }
}