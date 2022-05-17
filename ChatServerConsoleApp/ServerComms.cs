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
    public class ServerComms
    {
        public List<ClientInfo> threadList;
        public Dictionary<string, Account> accounts;
        private string[] helpcommands;
        private bool listening;

        public ServerComms()
        {
            threadList = new List<ClientInfo>();
            accounts = new Dictionary<string, Account>();
            helpcommands = new string[] { "/help", "/nickname", "/changenick", "/who", "/test" };

            listening = true;

            RetrieveAccountsFromDatabase();
        }

        public void RetrieveAccountsFromDatabase()
        {
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
                    accounts.Add((string)reader["Email"], new Account((string)reader["Email"], (string)reader["Password"], (string)reader["Username"]));
                }

                cmd.Dispose();
                connect.Close();
            }
        }

        public void StartListening()
        {
            
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, 8080);
                listener.Start();
                Console.WriteLine("Chat server started...");
                Console.WriteLine("Waiting for incoming client connections...");
                while (listening)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("Accepted new client connection!");
                    Thread t = new Thread(ProcessClientRequests);
                    t.IsBackground = true;
                    t.Start(client);


                }
            }
            catch (Exception e) { Console.WriteLine(e); }
            finally { if (listener != null) listener.Stop(); }
        }

        public void StopListening()
        {
            listening = false;
        }

        private void ProcessClientRequests(object argument)
        {
            TcpClient client = (TcpClient)argument;
            try
            {
                //Establish streamreader and -writer;
                StreamReader reader = new StreamReader(client.GetStream());
                StreamWriter writer = new StreamWriter(client.GetStream());
                string s = String.Empty;

                //Intercept first streamreader message from client, which will be the account name+password to check if account is registered and not yet logged in,
                //before proceeding;
                s = reader.ReadLine();

                string[] array = s.Split(',');

                if((accounts.ContainsKey(array[0]) && accounts[array[0]].password == array[1] && accounts[array[0]].online == false) )
                {

                    accounts[array[0]].online = true;
                    ClientInfo user = new ClientInfo(client, reader, writer, Thread.CurrentThread, accounts[array[0]].name);
                    threadList.Add(user);

                    while (!(s = reader.ReadLine()).Equals("/disconnect"))
                    {

                        Console.WriteLine("From {0} -> {1}", user.nick, s);

                        //Check if it's a PM;
                        if (s.StartsWith("/PM "))
                        {
                            StringBuilder sb = new StringBuilder();

                            //Get recipient off the PM;
                            string[] tempArray = s.Split(' ');

                            //Check if recipient is currently online;
                            int k = threadList.FindIndex(x => x.nick == tempArray[1]);

                            if(k >= 0)
                            {
                                try
                                {
                                    //Alter message to replace recipient with sender before forwarding to recipient;
                                    tempArray[1] = user.nick;


                                    for (int i = 0; i < tempArray.Count(); i++)
                                    {
                                        sb.Append(tempArray[i]);
                                        sb.Append(" ");
                                    }
                                    sb.Remove(sb.Length - 1, 1);

                                    threadList[k].writer.WriteLine(sb.ToString());
                                    threadList[k].writer.Flush();
                                }
                                catch (IOException e)
                                {
                                    Console.WriteLine("Problem with client communication (Private message). Exiting thread.");
                                    Console.WriteLine(e);
                                }
                                
                            }
                            else
                            {
                                //return a "recipient is offline" message;
                                //sb.Append(tempArray[0] + " ");
                                //sb.Append(tempArray[1] + " ");                                

                                try
                                {
                                    user.writer.WriteLine("ERROR002 " + tempArray[1]);
                                    user.writer.Flush();
                                }
                                catch (IOException e)
                                {
                                    Console.WriteLine("Problem with client communication (Private message (fail)). Exiting thread.");
                                    Console.WriteLine(e);
                                }

                            }

                        }
                        //Check for "/" commands, process them and send info back to just that client instead of echoing it to all clients;
                        else if (s.StartsWith("/"))
                        {
                            string temp = CheckForSlashCommands(s);
                            try
                            {
                                user.writer.WriteLine(temp);
                                user.writer.Flush();
                            }
                            catch (IOException e)
                            {
                                Console.WriteLine("Problem with client communication (Slash command). Exiting thread.");
                                Console.WriteLine(e);
                            }

                        }
                        else
                        {
                            for (int i = 0; i < threadList.Count; i++)
                            {
                                try
                                {
                                    threadList[i].writer.WriteLine(user.nick + ": " + s);
                                    threadList[i].writer.Flush();
                                }
                                catch (IOException e)
                                {
                                    Console.WriteLine("Problem with client communication. Exiting thread.");
                                    Console.WriteLine(e);
                                }

                            }
                        }



                        //writer.WriteLine("From server -> " + s);
                        //writer.Flush();

                    }
                    for (int i = 0; i < threadList.Count; i++)
                    {
                        if(threadList[i] == user)
                        {
                            threadList.Remove(threadList[i]);
                            accounts[array[0]].online = false;
                            i--;
                        }
                    }

                }
                else
                {
                    Console.WriteLine("Access denied to client due to incorrect account/password/status.");

                    try
                    {
                        writer.WriteLine("Error001");
                        writer.Flush();
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine("Problem with client communication (Error001). Exiting thread.");
                        Console.WriteLine(e);
                    }

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

        public string CheckForSlashCommands(string s)
        {
            switch (s)
            {
                case "/help":
                    return @"The following '/' commands exist: /help, /who, /test.";

                case "/who":
                    StringBuilder sb = new StringBuilder();
                    sb.Append("/who:");
                    foreach (var item in accounts)
                    {
                        if (item.Value.online)
                        {
                            sb.Append(item.Value.name + ",");
                        }
                    }
                    sb.Remove(sb.Length - 1, 1);
                    string temp = sb.ToString();
                    return temp;
                case "/test":

                    return "This is a test";


                default:
                    return "";
            }
        }

        public void CloseAllConnections()
        {
            for (int i = 0; i < threadList.Count; i++)
            {
                threadList[i].writer.WriteLine("Server is shutting down in 3 seconds.");
                threadList[i].writer.Flush();
                Console.WriteLine("Shutting down...");
                threadList[i].writer.WriteLine("Shutdown");
                threadList[i].writer.Flush();

                //This part isn't needed as "Shutdown" sent to clients will send a last "/disconnect" back to the server, covered in each client's thread's streamreader.
                //Thread.Sleep(1000);
                //threadList[i].reader.Close();
                //threadList[i].writer.Close();
                //threadList[i].client.Close();
                //threadList[i].client = null;                
            }

        }


    }
}
