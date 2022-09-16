using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace ChatServerConsoleApp
{
    public class PasswordHasher
    {

        // Hash the password+salt input and compare to the hashedpassword input, return wether they're equal or not;
        public bool CheckPassword(byte[] Salt, string Password, string HashedPassword)
        {
            return HashPassword(Password, Salt) == HashedPassword;
        }


        public byte[] GenerateSalt()
        {

            byte[] salt = new byte[128 / 8];
            using (var rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetNonZeroBytes(salt);
            }

            return salt;
        }


        public string HashPassword(string Password, byte[] Salt)
        {
            string hashedPassword = Convert.ToBase64String(KeyDerivation.Pbkdf2(Password, Salt, KeyDerivationPrf.HMACSHA256, 10000, 256 / 8));

            return hashedPassword;
        }

    }
}
