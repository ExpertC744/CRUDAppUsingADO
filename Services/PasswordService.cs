using System.Security.Cryptography;

namespace CRUDAppUsingADO.Services;

public class PasswordService : IPasswordService
{
    private const int SaltSize = 16;        // 128 bit
    private const int HashSize = 32;        // 256 bit
    private const int Iterations = 100_000; // OWASP recommended minimum for PBKDF2-SHA256

    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSize);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var salt = Convert.FromBase64String(storedSalt);
        var expectedHash = Convert.FromBase64String(storedHash);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSize);

        // Constant-time comparison prevents timing attacks
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", ""); // URL-safe
    }
}