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
//        public List<string> usernameList;
        private string[] helpcommands;
        private bool listening;
        public Security secureChat;
        PasswordHasher pwHasher;

        public ServerComms(Dictionary<string, Account> Accounts)
        {
            pwHasher = new PasswordHasher();
            secureChat = new Security();
            threadList = new List<ClientInfo>();
//            usernameList = new List<string>();
            accounts = Accounts;
            helpcommands = new string[] { "/help", "/nickname", "/changenick", "/who", "/test" };

            listening = true;

            RetrieveAccountsFromDatabase();
        }

        private void RetrieveAccountsFromDatabase()
        {

            foreach (var item in accounts)
            {
//                usernameList.Add(item.Value.name);
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
            ClientInfo user = null;
            try
            {
                //Establish streamreader and -writer;
                StreamReader reader = new StreamReader(client.GetStream());
                StreamWriter writer = new StreamWriter(client.GetStream());
                string s = String.Empty;

                //Send server's public key to the client before the client send the login validation info, so that the login info can be sent securely.
                BroadcastPublicKey(writer, "Global", secureChat.pubKeyAsString);

                //Intercept first streamreader message from client, which (ENCRYPTED) will be the account name and password 
                //to check if account is registered and not yet logged in before proceeding;
                s = reader.ReadLine();
                Console.WriteLine(s);
                string tems = secureChat.RSADecrypt(s);

                //[0] = email, [1] = password;
                string[] array = tems.Split(' ');


                if((accounts.ContainsKey(array[0]) && pwHasher.CheckPassword(accounts[array[0]].salt, array[1], accounts[array[0]].hashedPassword) && accounts[array[0]].online == false) )
                {

                    //Intercept SECOND streamreader message from client, which will be the public key's modulus and exponent;
                    s = reader.ReadLine();
                    //[0] = pub key's modulus, [1] = pub key's exponent.
                    string[] keyArray = s.Split(' ');

                    //Convert public key strings to byte[];
                    byte[] tempModulus = Array.ConvertAll(keyArray[0].Split(','), Byte.Parse);
                    byte[] tempExponent = Array.ConvertAll(keyArray[1].Split(','), Byte.Parse);

                    accounts[array[0]].online = true;
                    user = new ClientInfo(client, reader, writer, Thread.CurrentThread, accounts[array[0]].name, accounts[array[0]].email, tempModulus, tempExponent);
                    threadList.Add(user);


                    //Listen to stream until disconnected;
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

                            if (k >= 0)
                            {
                                try
                                {
                                    //If it's the first contact between the 2 this session, add PM recipient to sender account's contactList, and vice versa.
                                    if (!accounts[user.email].contactList.Contains(tempArray[1]))
                                    {
                                        accounts[user.email].contactList.Add(tempArray[1]);
                                        //accounts[tempArray[1]].contactList.Add(user.email);
                                    }

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
                        else if (s.StartsWith("/Global"))
                        {
                            s = s.Remove(0, 8);

                            //Decrypt the message using the server's private key;
                            string decryptedText = secureChat.RSADecrypt(s);

                            for (int i = 0; i < threadList.Count; i++)
                            {
                                //Encrypt the message (including the user's name) using each client's public key;
                                string encyptedText = secureChat.RSAEncrypt(user.nick + ": " + decryptedText, threadList[i].RSAParams);
                                
                                try
                                {
                                    threadList[i].writer.WriteLine("/Global " + encyptedText);
                                    threadList[i].writer.Flush();
                                }
                                catch (IOException e)
                                {
                                    Console.WriteLine("Problem with client communication. Exiting thread.");
                                    Console.WriteLine(e);
                                }

                            }
                        }
                        //Check for "/" commands, process them and send info back to just that client instead of echoing it to all clients;
                        else if (s.StartsWith("/"))
                        {
                            string temp = CheckForSlashCommands(s, user);
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
                Console.WriteLine("Problem with client communication (Client/User '{0}' forcefully cut connection). Exiting thread.", user != null? user.email : "unknown name");
                //Console.WriteLine(e);

                //Check if ClientInfo user was established before IOException occurred, remove from threadList if so and set the account's online status to false; 
                if (user != null)
                {
                    for (int i = 0; i < threadList.Count; i++)
                    {
                        if (threadList[i] == user)
                        {
                            threadList.Remove(threadList[i]);
                            accounts[user.email].online = false;
                            i--;
                        }
                    }
                }

            }
            finally
            {
                if (client != null) { client.Close(); }
            }
        }

        public void BroadcastPublicKey(ClientInfo recipient, string keyOwner, string publicKey)
        {
            try
            {
                recipient.writer.WriteLine("/PublicKey " + keyOwner + " " + publicKey);
                recipient.writer.Flush();
            }
            catch (IOException e)
            {
                Console.WriteLine("Problem with client communication (sending public key). Exiting thread.");
                Console.WriteLine(e);
            }
        }

        public void BroadcastPublicKey(StreamWriter writer, string keyOwner, string publicKey)
        {
            try
            {
                writer.WriteLine("/PublicKey " + keyOwner + " " + publicKey);
                writer.Flush();
            }
            catch (IOException e)
            {
                Console.WriteLine("Problem with client communication (sending public key). Exiting thread.");
                Console.WriteLine(e);
            }
        }

        public string CheckForSlashCommands(string s, ClientInfo sender)
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
                case string sTemp when s.StartsWith("/RequestKey"):
                    {
                        string[] array = s.Split(' ');
                        int i = threadList.FindIndex(x => x.nick == array[1]);
                        //If PM recipient is online, send recipient's public key to client requesting a PM, and vice versa;
                        if (i != -1)
                        {
                            BroadcastPublicKey(sender, threadList[i].nick, threadList[i].PublicKeyAsString());
                            BroadcastPublicKey(threadList[i], sender.nick, sender.PublicKeyAsString());
                            return "Public keys shared.";
                        }
                        //Else send offline error to the requesting client;
                        else
                        {
                            return "ERROR002 " + array[1];
                        }
                    }
                case string sTemp when s.StartsWith("/RequestMultipleKeys"):
                    {
                        string[] array = s.Split(' ');

                        for (int ji = 1; ji < array.Length; ji++)
                        {
                            int i = threadList.FindIndex(x => x.nick == array[ji]);
                            //If PM recipient is online, send recipient's public key to client requesting a PM, and vice versa;
                            if (i != -1)
                            {
                                BroadcastPublicKey(sender, threadList[i].nick, threadList[i].PublicKeyAsString());
                                BroadcastPublicKey(threadList[i], sender.nick, sender.PublicKeyAsString());
                                
                            }
                        }
                        return "Public keys shared of those who are online.";
                    }
                case "/RequestContactKeys":
                    List<string> tempList = accounts[sender.email].contactList;
                    
                    if(tempList.Count > 0)
                    {
                        for (int ji = 0; ji < tempList.Count; ji++)
                        {
                            int i = threadList.FindIndex(x => x.nick == tempList[ji]);
                            //If PM recipient is online, send recipient's public key to client requesting a PM, and vice versa;
                            if (i != -1)
                            {
                                BroadcastPublicKey(sender, threadList[i].nick, threadList[i].PublicKeyAsString());
                                BroadcastPublicKey(threadList[i], sender.nick, sender.PublicKeyAsString());

                            }
                        }
                        return "All public keys of this session's contacts updated.";
                    }
                    else
                    {
                        return "No PM history this sesssion.";
                    }
                    
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
