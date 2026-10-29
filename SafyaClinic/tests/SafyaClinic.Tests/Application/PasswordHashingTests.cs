using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.Security;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>S5 / F03: Identity-compatible PBKDF2; old hashes still verify and get upgraded.</summary>
public class PasswordHashingTests
{
    private const string Password = "Correct-horse-1";

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        Assert.Equal(PasswordCheck.Success, PasswordHashing.Verify(Password, PasswordHashing.Hash(Password)));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        Assert.Equal(PasswordCheck.Failed, PasswordHashing.Verify("wrong-password", PasswordHashing.Hash(Password)));
    }

    [Theory]
    [InlineData("not-a-valid-hash")]
    [InlineData("")]
    [InlineData("AAAA")]
    [InlineData("x.y.z")]
    public void Invalid_stored_hash_fails(string storedHash)
    {
        Assert.Equal(PasswordCheck.Failed, PasswordHashing.Verify(Password, storedHash));
    }

    [Fact]
    public void Hashes_are_salted()
    {
        Assert.NotEqual(PasswordHashing.Hash(Password), PasswordHashing.Hash(Password));
    }

    [Fact]
    public void Old_format_verifies_and_asks_for_a_new_hash()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Password).Concat(salt).ToArray());
        var legacy = Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);

        Assert.Equal(PasswordCheck.SuccessRehashNeeded, PasswordHashing.Verify(Password, legacy));
        Assert.Equal(PasswordCheck.Failed, PasswordHashing.Verify("wrong-password", legacy));
    }

    [Fact]
    public void Identity_password_hasher_accepts_our_hashes()
    {
        var identity = new PasswordHasher<object>();

        Assert.Equal(PasswordVerificationResult.Success,
            identity.VerifyHashedPassword(new object(), PasswordHashing.Hash(Password), Password));
    }

    [Fact]
    public void Identity_hashes_are_accepted_and_weaker_ones_are_upgraded()
    {
        var strong = new PasswordHasher<object>().HashPassword(new object(), Password);
        var weak = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }))
            .HashPassword(new object(), Password);

        Assert.Equal(PasswordCheck.Success, PasswordHashing.Verify(Password, strong));
        Assert.Equal(PasswordCheck.SuccessRehashNeeded, PasswordHashing.Verify(Password, weak));
    }

    [Theory]
    [InlineData("short", "short")]
    [InlineData("long-enough-1", "different-12")]
    [InlineData("", "")]
    public void Weak_or_mismatched_new_passwords_are_refused(string password, string confirmation)
    {
        Assert.NotNull(PasswordHashing.Validate(password, confirmation));
    }

    [Fact]
    public void Ten_character_matching_password_is_accepted()
    {
        Assert.Null(PasswordHashing.Validate("0123456789", "0123456789"));
    }
}