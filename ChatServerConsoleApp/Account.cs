using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServerConsoleApp
{
    public class Account : IEquatable<Account>
    {
        public List<string> contactList;
        public string email, hashedPassword, name;
        public bool online;
        public byte[] salt;


        public Account(string Email, string Name, string HashedPassword, string SaltString)
        {
            contactList = new List<string>();
            email = Email;
            hashedPassword = HashedPassword;
            name = Name;
            online = false;
            salt = ConvertedSaltString(SaltString);           
        }

        private byte[] ConvertedSaltString(string SaltString)
        {
            byte[] salt = Array.ConvertAll(SaltString.Split(','), Byte.Parse);
            return salt;
        }

        public bool Equals(Account other)
        {
            return (this.email == other.email);
        }
    }
}
