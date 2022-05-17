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
    public class Registration
    {
        bool listening;

        public Registration()
        {
            listening = true;
        }



        public void StartListening()
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, 8081);
                listener.Start();
                Console.WriteLine("Waiting for incoming register requests...");
                while (listening)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("Incoming registration request!");

                    ProcessRegistrationRequests(client);

                }
            }
            catch (Exception e) { Console.WriteLine(e); }
            finally { if (listener != null) listener.Stop(); }
        }

        public void ProcessRegistrationRequests(object argument)
        {
            TcpClient client = (TcpClient)argument;
            string email = "";
            string password = "";
            string username = "";

            try
            {
                //Establish streamreader and -writer;
                StreamReader reader = new StreamReader(client.GetStream());
                StreamWriter writer = new StreamWriter(client.GetStream());
                string s = String.Empty;

                //Read the stream and process it accordingly (only 1 readline sent for registering);
                s = reader.ReadLine();

                string[] array = s.Split(',');
                email = array[0];
                password = array[1];
                username = array[2];


                if ((!Program.comms.accounts.ContainsKey(email)))
                {
                    writer.WriteLine("Registration succesful");
                    writer.Flush();

                    //Add account to comms' dictionary;
                    Program.comms.accounts.Add(email, new Account(email, password, username));

                    //Add account to database;
                    string path = Directory.GetCurrentDirectory();
                    path += @"\TestDatabase.sqlite";

                    using (SQLiteConnection connect = new SQLiteConnection("Data Source=" + path + "; Version=3;"))
                    {
                        connect.Open();
                        string str = "INSERT INTO Accounts (Email, Password, Username) VALUES ('" + email + "', '" + password + "', '" + username + "')";
                        SQLiteCommand cmd = new SQLiteCommand(str, connect);
                        cmd.ExecuteNonQuery();

                        cmd.Dispose();
                        connect.Close();
                    }

                }
                else
                {
                    writer.WriteLine("Registration failed, email already used.");
                    writer.Flush();
                }





                reader.Close();
                writer.Close();
                client.Close();
                client = null;
                Console.WriteLine("Client connection closed!");

            }
            catch (IOException e)
            {
                Console.WriteLine("Problem with client communication. Exiting thread.");
                Console.WriteLine(e);
            }
            finally
            {
                if (client != null) { client.Close(); }
            }

        }

    }
}
