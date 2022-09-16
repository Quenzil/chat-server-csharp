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
        TcpListenerWrapper listener;
        PasswordHasher pwHasher;

        Dictionary<string, NameIdPair> accountNamesAndID;
        Dictionary<int, string> sameNameIDs;

        public Registration(Dictionary<string, Account> Accounts)
        {
            listener = null;
            pwHasher = new PasswordHasher();
            accountNamesAndID = new Dictionary<string, NameIdPair>();
            sameNameIDs = new Dictionary<int, string>();
            FillAccountNames(Accounts);
        }


        public void AddAccountToDatabaseAndDictionary(string email, string username, string password)
        {
            //Generate salt + saltString;
            byte[] salt = pwHasher.GenerateSalt();
            string saltString;

            StringBuilder sb = new StringBuilder();
            foreach (var item in salt)
            {
                sb.Append(item.ToString() + ",");
            }
            sb.Remove(sb.Length - 1, 1);

            saltString = sb.ToString();

            //Hash the password;
            string hashedPassword = pwHasher.HashPassword(password, salt);

            //Add account to database;
            string path = Directory.GetCurrentDirectory();
            path += @"\TestDatabase.sqlite";

            using (SQLiteConnection connect = new SQLiteConnection("Data Source=" + path + "; Version=3;"))
            {
                connect.Open();
                string str = "INSERT INTO Accounts (Email, Username, HashedPassword, Salt) VALUES ('" + email + "', '" + username + "', '" + hashedPassword + "', '" + saltString + "')";
                SQLiteCommand cmd = new SQLiteCommand(str, connect);
                cmd.ExecuteNonQuery();

                cmd.Dispose();
                connect.Close();
            }

            //Add account to comms' dictionary;
            Program.comms.accounts.Add(email, new Account(email, username, hashedPassword, saltString));
        }

        //rudimentary email validity check;
        public bool CheckForEmailValidity(string email)
        {
            int at = email.IndexOf("@");
            int dot = email.LastIndexOf(".");

            if (at > 0 && dot > (at + 1))
            {
                return true;
            }
            else return false;
        }

        private string CreateUniqueUserName(string username)
        {
            sameNameIDs.Clear();
            //populate sameNameIDs with ID keys and same as the registering username's name values (e.g. all existing "Bob"'s and their ID #s);
            foreach (var item in accountNamesAndID)
            {
                if (item.Value.name == username)
                {
                    sameNameIDs.Add(item.Value.id, item.Value.name);
                }
            }

            string temp = "";

            //Add a unique #identifier to the name;
            if (sameNameIDs.Count == 0)
            {
                temp = username + "#0001";
                accountNamesAndID.Add(temp, new NameIdPair(username, 1));
            }
            else
            {
                for (int i = 1; i < 10000; i++)
                {
                    if (!sameNameIDs.ContainsKey(i))
                    {
                        temp = username + "#" + i.ToString("D4");
                        accountNamesAndID.Add(temp, new NameIdPair(username, i));
                        break;
                    }
                }
            }
            if (String.IsNullOrEmpty(temp))
            {
                temp = "ERROR1";
            }

            return temp;
        }

        private void FillAccountNames(Dictionary<string, Account> Accounts)
        {
            foreach (var item in Accounts)
            {
                int id;
                string name, tempID;
                string nameID = item.Value.name;
                int separator = nameID.IndexOf('#');
                if (separator >= 0)
                {
                    name = nameID.Substring(0, separator);
                    tempID = nameID.Remove(0, separator + 1);
                    id = Convert.ToInt32(tempID);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Registration.cs, account name does not contain # which it should.");
                    continue;
                }

                accountNamesAndID.Add(nameID, new NameIdPair(name, id));
            }
        }

        public void ProcessRegistrationRequests(object argument)
        {
            TcpClient client = (TcpClient)argument;

            try
            {
                //Establish streamreader and -writer;
                StreamReader reader = new StreamReader(client.GetStream());
                StreamWriter writer = new StreamWriter(client.GetStream());
                string s = String.Empty;

                //Write server's public key to the connected client for secure registration;
                writer.WriteLine(Program.comms.secureChat.PublicKeyAsString());
                writer.Flush();

                //Read the stream and process it accordingly (only 1 readline sent for registering), including decrypting it;
                s = reader.ReadLine();

                string sDecrypted = Program.comms.secureChat.RSADecrypt(s);

                string[] array = sDecrypted.Split(',');
                string email = array[0];
                string password = array[1];
                string username = array[2];

                if ((!Program.comms.accounts.ContainsKey(email) && CheckForEmailValidity(email)))
                {
                    string uniqueUsername = CreateUniqueUserName(username);

                    if (uniqueUsername == "ERROR1")
                    {
                        writer.WriteLine("Registration failed, name's count limit reached. Please try a different name.");
                        writer.Flush();
                    }
                    else
                    {
                        AddAccountToDatabaseAndDictionary(email, uniqueUsername, password);

                        writer.WriteLine("Registration successful.");
                        writer.Flush();
                    }
                }
                else if (!CheckForEmailValidity(email))
                {
                    writer.WriteLine("Registration failed, invalid email format used.");
                    writer.Flush();
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
                Console.WriteLine("Client connection (resistration) closed!");
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

        public void StartListening()
        {
            try
            {
                listener = new TcpListenerWrapper(IPAddress.Any, 8081);
                listener.Start();
                Console.WriteLine("Waiting for incoming register requests...");
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("Incoming registration request!");

                    ProcessRegistrationRequests(client);

                }
            }
            catch (SocketException e)
            {
                if (!listener.Active)
                {
                    Console.WriteLine("TCP listener of Registration stopped listening.");
                }
                else { Console.WriteLine(e); }
            }
            //catch (Exception e) { Console.WriteLine(e); }
            finally { if (listener != null) listener.Stop(); }
        }


        public void StopListeningForConnections()
        {
            listener.Stop();
        }
    }
}
