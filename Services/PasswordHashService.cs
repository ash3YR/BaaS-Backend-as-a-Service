using System.Security.Cryptography;

namespace BaaS.Services;

public class PasswordHashService
{
    public (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(
            hashBytes,
            Convert.FromBase64String(hash));
    }
}
