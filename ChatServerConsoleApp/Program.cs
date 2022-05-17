using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Data.SQLite;

namespace ChatServerConsoleApp
{
    class Program
    {
        
        //private static Dictionary<string, bool> tempCheck = new Dictionary<string, bool>();
        public static ServerComms comms = new ServerComms();
        private static bool running = true;
        static SQLiteCommand command = new SQLiteCommand();

        public static Registration rComms = new Registration();

        static void CreateDatabaseIfNotExists()
        {
            string path = Directory.GetCurrentDirectory();
            path += @"\TestDatabase.sqlite";

            if (!File.Exists(path))
            {
                SQLiteConnection.CreateFile("TestDatabase.sqlite");
            }

            
            
            using (SQLiteConnection connect = new SQLiteConnection("Data Source=" + path + "; Version=3;"))
            {
                connect.Open();
                SQLiteCommand createTable = new SQLiteCommand();
                createTable = connect.CreateCommand();
                createTable.CommandText = "CREATE TABLE IF NOT EXISTS Accounts (Email TEXT UNIQUE, Password TEXT, Username TEXT)";
                createTable.ExecuteNonQuery();
                createTable.Dispose();
                connect.Close();
            }
            
        }

        //Filling of 'Accounts' table for testing purposes and initial "signed up accounts";
        static void PopulateTable()
        {
            string path = Directory.GetCurrentDirectory();
            path += @"\TestDatabase.sqlite";

            using (SQLiteConnection connect = new SQLiteConnection("Data Source=" + path + "; Version=3;"))
            {
                connect.Open();
                SQLiteCommand cmd = new SQLiteCommand("INSERT INTO Accounts (Email, Password, Username)" +
                    "VALUES ('a', '1', 'Bob')", connect);
                cmd.ExecuteNonQuery();
                cmd = new SQLiteCommand("INSERT INTO Accounts (Email, Password, Username)" +
                    "VALUES ('b', '2', 'Rick')", connect);
                cmd.ExecuteNonQuery();
                cmd = new SQLiteCommand("INSERT INTO Accounts (Email, Password, Username)" +
                    "VALUES ('c', '3', 'Tina')", connect);
                cmd.ExecuteNonQuery();
                cmd = new SQLiteCommand("INSERT INTO Accounts (Email, Password, Username)" +
                    "VALUES ('d', '4', 'Clara')", connect);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                connect.Close();
            }
        }

        //Clearing of 'Accounts' table for testing purposes;
        static void ClearTable()
        {
            string path = Directory.GetCurrentDirectory();
            path += @"\TestDatabase.sqlite";

            using (SQLiteConnection connect = new SQLiteConnection("Data Source=" + path + "; Version=3;"))
            {
                connect.Open();
                SQLiteCommand cmd = new SQLiteCommand("DELETE FROM Accounts", connect);
                cmd.ExecuteNonQuery();
                cmd.Dispose();
                connect.Close();
            }
        }

        //Printing of 'Accounts' table in Console.WriteLine for testing purposes;
        static void PrintAllDatabaseData()
        {
            string path = Directory.GetCurrentDirectory();
            path += @"\TestDatabase.sqlite";

            using (SQLiteConnection connect = new SQLiteConnection("Data Source=" + path + "; Version=3;"))
            {
                connect.Open();
                string str = "SELECT * FROM Accounts";
                SQLiteCommand cmd = new SQLiteCommand(str, connect);
                SQLiteDataReader reader = cmd.ExecuteReader();
                //cmd.ExecuteNonQuery();

                Console.WriteLine("-----");
                while (reader.Read())
                {
                    Console.WriteLine("Email:  {0},  Password:  {1},  Username:  {2}.", reader["Email"], reader["Password"], reader["Username"]);
                }
                Console.WriteLine("-----");

                cmd.Dispose();
                connect.Close();
            }
        }

        static async Task ReadConsoleAsync()
        {
            await Task.Run(() =>
            {
                while (running)
                {
                    string temp = Console.ReadLine();
                    ServerCommands(temp);
                    //Thread.Sleep(3000);
                    //comms.StopListening();
                    //running = false;
                }

            });
        }

        static async Task StartRegistrationsAsync()
        {
            await Task.Run(() =>
            {
                rComms.StartListening();
            });
        }

        private static void ServerCommands(string s)
        {
            if (s == "Stop")
            {
                Console.WriteLine("Some closing code");
                comms.CloseAllConnections();
            }
            else if (s == "Count")
            {
                Console.WriteLine(comms.threadList.Count());
            }
            else if(s == "Fill")
            {
                PopulateTable();
            }
            else if (s == "Print")
            {
                PrintAllDatabaseData();
            }
            else if(s == "Clear")
            {
                ClearTable();
            }
        }


        static void Main(string[] args)
        {
            //Console.WriteLine("Current directory is {0}", Directory.GetCurrentDirectory().ToString());
            CreateDatabaseIfNotExists();
            ReadConsoleAsync();
            StartRegistrationsAsync();

            comms.StartListening();
            
            Console.ReadLine();
        }



    }

}

