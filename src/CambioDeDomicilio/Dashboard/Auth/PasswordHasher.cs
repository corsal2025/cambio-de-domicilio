using System.Security.Cryptography;

namespace CambioDeDomicilio.Dashboard.Auth;

/// <summary>PBKDF2 with a per-user random salt. No ASP.NET Identity — this is one table and two functions.</summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    // OWASP (2023+) recommends 600k+ iterations for PBKDF2-HMAC-SHA256. Safe to raise: the
    // iteration count is stored alongside each hash (see Verify) and used for verification,
    // so existing hashes keep working with their original count — only new hashes use this value.
    public const int DefaultIterations = 600_000;

    public static (string Hash, string Salt, int Iterations) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt), DefaultIterations);
    }

    public static bool Verify(string password, string storedHash, string storedSalt, int iterations)
    {
        var salt = Convert.FromBase64String(storedSalt);
        var expected = Convert.FromBase64String(storedHash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
