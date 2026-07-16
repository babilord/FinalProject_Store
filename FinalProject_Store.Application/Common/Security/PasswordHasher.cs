using FinalProject_Store.Application.Interfaces.Security;
using System.Security.Cryptography;

namespace FinalProject_Store.Application.Common.Security
{
    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;

        private const string AlgorithmName = "PBKDF2";
        private const string Version = "V1";

        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException(
                    "Password cannot be empty.",
                    nameof(password));
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password: password,
                salt: salt,
                iterations: Iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: KeySize);

            return string.Join(
                "$",
                AlgorithmName,
                Version,
                Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        public bool VerifyPassword(
            string hashedPassword,
            string providedPassword)
        {
            if (string.IsNullOrWhiteSpace(hashedPassword) ||
                string.IsNullOrWhiteSpace(providedPassword))
            {
                return false;
            }

            if (!IsHashed(hashedPassword))
            {
                return false;
            }

            try
            {
                string[] parts = hashedPassword.Split('$');

                if (parts.Length != 5)
                {
                    return false;
                }

                if (!int.TryParse(parts[2], out int iterations))
                {
                    return false;
                }

                byte[] salt = Convert.FromBase64String(parts[3]);
                byte[] expectedHash = Convert.FromBase64String(parts[4]);

                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    password: providedPassword,
                    salt: salt,
                    iterations: iterations,
                    hashAlgorithm: HashAlgorithmName.SHA256,
                    outputLength: expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(
                    actualHash,
                    expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public bool IsHashed(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            return password.StartsWith(
                $"{AlgorithmName}${Version}$",
                StringComparison.Ordinal);
        }
    }
}