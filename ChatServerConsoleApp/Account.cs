using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServerConsoleApp
{
    public class Account : IEquatable<Account>
    {
        public string email, name, hashedPassword;
        public byte[] salt;
        public bool online;
        public List<string> contactList;

        public Account(string Email, string Name, string HashedPassword, string SaltString)
        {
            email = Email;
            hashedPassword = HashedPassword;
            name = Name;
            salt = ConvertedSaltString(SaltString);
            online = false;
            contactList = new List<string>();
        }

        public bool Equals(Account other)
        {
            return (this.email == other.email);
        }

        private byte[] ConvertedSaltString(string SaltString)
        {
            byte[] salt = Array.ConvertAll(SaltString.Split(','), Byte.Parse);
            return salt;
        }
    }
}
