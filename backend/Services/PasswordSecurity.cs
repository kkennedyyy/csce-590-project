using System.Security.Cryptography;

namespace ClassFinder.Api.Services;

internal static class PasswordSecurity
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string storedPassword, string candidatePassword, string fallbackPassword)
    {
        if (string.IsNullOrWhiteSpace(candidatePassword))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(storedPassword))
        {
            return string.Equals(candidatePassword, fallbackPassword, StringComparison.Ordinal);
        }

        if (!storedPassword.StartsWith($"{Prefix}$", StringComparison.Ordinal))
        {
            return string.Equals(storedPassword, candidatePassword, StringComparison.Ordinal);
        }

        var parts = storedPassword.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                candidatePassword,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length
            );

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
