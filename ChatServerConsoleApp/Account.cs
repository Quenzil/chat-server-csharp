using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServerConsoleApp
{
    public class Account : IEquatable<Account>
    {
        public string email, password, name;
        public bool online;

        public Account(string Email, string Password, string Name)
        {
            email = Email;
            password = Password;
            name = Name;
            online = false;
        }

        public bool Equals(Account other)
        {
            return (this.email == other.email);
        }
    }
}
