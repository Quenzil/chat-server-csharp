using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServerConsoleApp
{
    public class NameIdPair
    {
        public int id;
        public string name;

        public NameIdPair(string Name, int ID)
        {           
            id = ID;
            name = Name;
        }


    }
}
