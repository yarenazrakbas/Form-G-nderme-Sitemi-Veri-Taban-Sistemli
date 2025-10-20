using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace ITAnket.Security
{
    public interface IPasswordHasher
    {
        (byte[] hash, byte[] salt) Hash(string password, int iterations);
        bool Verify(string password, byte[] hash, byte[] salt, int iterations);
    }

    public class Pbkdf2PasswordHasher : IPasswordHasher
    {
        public (byte[] hash, byte[] salt) Hash(string password, int iterations)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, 32);
            return (hash, salt);
        }

        public bool Verify(string password, byte[] hash, byte[] salt, int iterations)
        {
            var check = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, 32);
            return CryptographicOperations.FixedTimeEquals(hash, check);
        }
    }

    public class AdminSeedOptions
    {
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "aspilicBilgiislem123";
        public int Iterations { get; set; } = 100000;
    }

    public class DuplicatePolicyOptions
    {
        public int BlockDays { get; set; } = 30; // aynı e-postayla 30 gün içinde tekrar engelle
    }
}
