using System.Security.Cryptography;
using System.Text;

namespace SafyaClinic.Application.Security;

/// <summary>
/// Password hashing in the ASP.NET Core Identity v3 format (PBKDF2-HMAC-SHA512,
/// 100,000 iterations, 128-bit salt, 256-bit key), so stored hashes stay compatible
/// with Identity's PasswordHasher if the app moves to it later (slice S5).
/// Also verifies the old "salt.sha256" format so existing users can still sign in;
/// <see cref="PasswordCheck.SuccessRehashNeeded"/> tells the caller to store a new hash.
/// </summary>
public static class PasswordHashing
{
    public const int MinimumLength = 10;

    private const byte FormatMarker = 0x01;     // Identity v3
    private const uint PrfSha512 = 2;           // KeyDerivationPrf.HMACSHA512
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int HeaderSize = 13;          // marker + prf + iterations + salt length

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, KeySize);

        var output = new byte[HeaderSize + SaltSize + KeySize];
        output[0] = FormatMarker;
        WriteUInt32(output, 1, PrfSha512);
        WriteUInt32(output, 5, Iterations);
        WriteUInt32(output, 9, SaltSize);
        salt.CopyTo(output, HeaderSize);
        key.CopyTo(output, HeaderSize + SaltSize);
        return Convert.ToBase64String(output);
    }

    public static PasswordCheck Verify(string? password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return PasswordCheck.Failed;

        // Base64 never contains '.', the old format always does.
        if (storedHash.Contains('.'))
            return VerifyLegacy(password, storedHash) ? PasswordCheck.SuccessRehashNeeded : PasswordCheck.Failed;

        try
        {
            var bytes = Convert.FromBase64String(storedHash);
            if (bytes.Length < HeaderSize || bytes[0] != FormatMarker) return PasswordCheck.Failed;

            var prf = ReadUInt32(bytes, 1);
            var iterations = (int)ReadUInt32(bytes, 5);
            var saltLength = (int)ReadUInt32(bytes, 9);
            HashAlgorithmName? algorithm = prf switch
            {
                0 => HashAlgorithmName.SHA1,
                1 => HashAlgorithmName.SHA256,
                2 => HashAlgorithmName.SHA512,
                _ => null
            };
            if (algorithm is null || iterations <= 0 || saltLength < 16) return PasswordCheck.Failed;

            var keyLength = bytes.Length - HeaderSize - saltLength;
            if (keyLength < 16) return PasswordCheck.Failed;

            var salt = bytes.AsSpan(HeaderSize, saltLength).ToArray();
            var expected = bytes.AsSpan(HeaderSize + saltLength, keyLength).ToArray();
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, algorithm.Value, keyLength);

            if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return PasswordCheck.Failed;

            return prf == PrfSha512 && iterations >= Iterations
                ? PasswordCheck.Success
                : PasswordCheck.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed;
        }
    }

    /// <summary>Returns a message for the user, or null when the new password is acceptable.</summary>
    public static string? Validate(string? password, string? confirmation)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
            return $"The password must be at least {MinimumLength} characters.";
        if (password != confirmation)
            return "The password and its confirmation do not match.";
        return null;
    }

    // Format used before S5: base64(salt) + "." + base64(SHA-256(password + salt)).
    private static bool VerifyLegacy(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password).Concat(salt).ToArray());
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint ReadUInt32(byte[] buffer, int offset) =>
        (uint)(buffer[offset] << 24 | buffer[offset + 1] << 16 | buffer[offset + 2] << 8 | buffer[offset + 3]);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded
}