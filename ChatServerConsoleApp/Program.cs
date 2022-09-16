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
        static bool running = true;
        //static SQLiteCommand command = new SQLiteCommand();
        public static Dictionary<string, Account> accounts = RetrieveAccountsFromDatabase();

        public static ServerComms comms = new ServerComms(accounts);
        public static Registration rComms = new Registration(accounts);

        public static void CreateDatabaseIfNotExists()
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
                createTable.CommandText = "CREATE TABLE IF NOT EXISTS Accounts (Email TEXT UNIQUE, Username TEXT UNIQUE, HashedPassword TEXT, Salt TEXT)";
                createTable.ExecuteNonQuery();
                createTable.Dispose();
                connect.Close();
            }          
        }


        static private Dictionary<string, Account> RetrieveAccountsFromDatabase()
        {
            Dictionary<string, Account> temp = new Dictionary<string, Account>();

            string path = Directory.GetCurrentDirectory();
            path += @"\TestDatabase.sqlite";

            using (SQLiteConnection connect = new SQLiteConnection("Data Source=" + path + "; Version=3;"))
            {
                connect.Open();
                string str = "SELECT * FROM Accounts";
                SQLiteCommand cmd = new SQLiteCommand(str, connect);
                SQLiteDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    temp.Add((string)reader["Email"], new Account((string)reader["Email"], (string)reader["Username"], (string)reader["HashedPassword"], (string)reader["Salt"]));
                }

                cmd.Dispose();
                connect.Close();
            }

            return temp;
        }

        // Filling of 'Accounts' table for testing purposes, initial "signed up accounts";
        static void PopulateDatabaseTableAndAccountsDictionay()
        {
            rComms.AddAccountToDatabaseAndDictionary("a", "Bob#1001", "1");
            rComms.AddAccountToDatabaseAndDictionary("b", "Rick#1002", "2");
            rComms.AddAccountToDatabaseAndDictionary("c", "Tina#1003", "3");
            rComms.AddAccountToDatabaseAndDictionary("d", "Clara#1004", "4");
        }

        // Clearing of 'Accounts' table, as well as comms.accounts dictionary, for testing purposes;
        static void ClearDatabaseTableAndAccountsDictionary()
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

            comms.accounts.Clear();
        }

        // Printing of 'Accounts' table in Console.WriteLine for testing purposes;
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
                    Console.WriteLine("Email:  {0},  Username:  {1},  HashedPassword:  {2},  Salt:  {3}.", reader["Email"], reader["Username"], reader["HashedPassword"], reader["Salt"]);
                }
                Console.WriteLine("-----");

                cmd.Dispose();
                connect.Close();
            }
        }

        static async void ReadConsoleAsync()
        {
            await Task.Run(() =>
            {
                while (running)
                {
                    string temp = Console.ReadLine();
                    ServerCommands(temp);
                }

            });
        }

        static async void StartRegistrationsAsync()
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
                Console.WriteLine("Server is shutting down, please hold.");
                rComms.StopListeningForConnections();
                comms.CloseAllConnections();
                running = false;
            }
            else if (s == "Count")
            {
                Console.WriteLine(comms.threadList.Count());
            }
            else if(s == "Fill")
            {
                PopulateDatabaseTableAndAccountsDictionay();
            }
            else if (s == "Print")
            {
                PrintAllDatabaseData();
            }
            else if(s == "Clear")
            {
                ClearDatabaseTableAndAccountsDictionary();
            }
        }


        static void Main(string[] args)
        {
            CreateDatabaseIfNotExists();
            ReadConsoleAsync();
            StartRegistrationsAsync();

            comms.StartListening();

            Console.WriteLine("Server if now offline.");
            Console.ReadLine();
        }
    }
}

