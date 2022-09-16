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
        public Dictionary<string, Account> accounts;
        TcpListenerWrapper listener;
        bool listening;
        PasswordHasher pwHasher;
        public Security secureChat;
        public List<ClientInfo> threadList;

        public ServerComms(Dictionary<string, Account> Accounts)
        {
            accounts = Accounts;
            listener = null;
            listening = true;
            pwHasher = new PasswordHasher();
            secureChat = new Security();
            threadList = new List<ClientInfo>();
        }


        public void BroadcastPublicKey(ClientInfo recipient, string keyOwner, string publicKey)
        {
            try
            {
                SendMessage(recipient.writer, "/PublicKey " + keyOwner + " " + publicKey);
            }
            catch (IOException) { Console.WriteLine("Problem with client communication (sending public key). Exiting thread."); }
            //catch (Exception e) { Console.WriteLine(e); }
        }


        public void BroadcastServerPublicKey(StreamWriter writer, string keyOwner, string publicKey)
        {
            try
            {
                writer.WriteLine("/PublicKey " + keyOwner + " " + publicKey);
                writer.Flush();
            }
            catch (IOException) { Console.WriteLine("Problem with client communication (sending public key). Exiting thread."); }
            //catch (Exception e) { Console.WriteLine(e); }
        }


        public string CheckForSlashCommands(string s, ClientInfo sender)
        {
            switch (s)
            {
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
                case "/RequestContactKeys":
                    List<string> tempList = accounts[sender.email].contactList;

                    if (tempList.Count > 0)
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
            Console.WriteLine("Shutting down...");
            for (int i = 0; i < threadList.Count; i++)
            {
                string encyptedText = secureChat.RSAEncrypt("-- HOST: Server is shutting down in 10 seconds! --", threadList[i].RSAParams);

                SendMessage(threadList[i].writer, "/Global " + encyptedText);
            }

            Thread.Sleep(10000);
            StopListening();
            Thread.Sleep(1000);

            for (int i = 0; i < threadList.Count; i++)
            {
                try
                {
                    SendMessage(threadList[i].writer, "Shutdown");
                }
                catch (ObjectDisposedException) { Console.WriteLine("client already logged off."); }
                //catch (Exception e) { System.Diagnostics.Debug.WriteLine(e); }

            }

            // Set all accounts to offline;
            foreach (var account in accounts)
            {
                account.Value.online = false;
            }
        }


        private ClientInfo ProcessClientInfo(TcpClient client, StreamReader reader, StreamWriter writer, string emailString)
        {
            // Intercept SECOND streamreader message from client, which will be the public key's modulus and exponent;
            string s = reader.ReadLine();
            // [0] = pub key's modulus, [1] = pub key's exponent.
            string[] keyArray = s.Split(' ');

            // Convert public key strings to byte[];
            byte[] tempModulus = Array.ConvertAll(keyArray[0].Split(','), Byte.Parse);
            byte[] tempExponent = Array.ConvertAll(keyArray[1].Split(','), Byte.Parse);

            accounts[emailString].online = true;
            ClientInfo user = new ClientInfo(client, reader, writer, Thread.CurrentThread, accounts[emailString].name, accounts[emailString].email, tempModulus, tempExponent);
            threadList.Add(user);

            return user;
        }


        private void ProcessClientRequests(object argument)
        {
            Console.WriteLine("Accepted new client connection!");

            TcpClient client = (TcpClient)argument;
            ClientInfo user = null;
            try
            {
                // Establish streamreader and -writer;
                StreamReader reader = new StreamReader(client.GetStream());
                StreamWriter writer = new StreamWriter(client.GetStream());
                string s = String.Empty;

                // Send server's public key to the client before the client send the login validation info, so that the login info can be sent securely.
                BroadcastServerPublicKey(writer, "Global", secureChat.pubKeyAsString);

                // Intercept first streamreader message from client, which (ENCRYPTED) will be the account name and password 
                // to check if account is registered and not yet logged in before proceeding;
                s = reader.ReadLine();
                string sDecrypted = secureChat.RSADecrypt(s);

                // [0] = email, [1] = password;
                string[] array = sDecrypted.Split(' ');
                string email = array[0];

                if ((accounts.ContainsKey(email) && pwHasher.CheckPassword(accounts[email].salt, array[1], accounts[email].hashedPassword) && accounts[email].online == false))
                {

                    user = ProcessClientInfo(client, reader, writer, email);

                    // Listen to stream until disconnected or server shutting down;
                    while (listening && !(s = user.reader.ReadLine()).Equals("/disconnect"))
                    {
                        ProcessClientCommunications(user, s);
                    }

                    if (listening)
                    {
                        ProcessClientEndOfCommunications(user);
                    }
                    
                }
                else
                {
                    Console.WriteLine("Access denied to client due to incorrect account/password/status.");
                    try
                    {
                        SendMessage(writer, "Error001");
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Problem with client communication (Error001). Exiting thread.");
                    }
                }

                reader.Close();
                writer.Close();
                client.Close();
                client = null;
                Console.WriteLine("Client connection closed!");
            }
            catch (IOException)
            {
                Console.WriteLine("Problem with client communication (Client/User '{0}' forcefully cut connection). Exiting thread.", user != null ? user.email : "unknown name");
                //Console.WriteLine(e);

                // Check if ClientInfo user was established before IOException occurred, remove from threadList if so and set the account's online status to false; 
                if (user != null)
                {
                    ProcessClientEndOfCommunications(user);
                }

            }
            finally
            {
                if (client != null) { client.Close(); }
            }
        }


        public void ProcessClientCommunications(ClientInfo user, string s)
        {
            // Check if it's a PM;
            if (s.StartsWith("/PM "))
            {
                StringBuilder sb = new StringBuilder();

                // Get recipient off the PM;
                string[] tempArray = s.Split(' ');

                // Check if recipient is currently online;
                int k = threadList.FindIndex(x => x.nick == tempArray[1]);

                if (k >= 0)
                {
                    try
                    {
                        // If it's the first contact between the 2 this session, add PM recipient to sender account's contactList, and vice versa.
                        if (!accounts[user.email].contactList.Contains(tempArray[1]))
                        {
                            accounts[user.email].contactList.Add(tempArray[1]);
                        }

                        // Alter message to replace recipient with sender before forwarding to recipient;
                        tempArray[1] = user.nick;

                        for (int i = 0; i < tempArray.Count(); i++)
                        {
                            sb.Append(tempArray[i]);
                            sb.Append(" ");
                        }
                        sb.Remove(sb.Length - 1, 1);

                        SendMessage(threadList[k].writer, sb.ToString());
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Problem with client communication (Private message). Exiting thread.");
                    }

                }
                else
                {
                    try
                    {
                        SendMessage(user.writer, "ERROR002 " + tempArray[1]);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Problem with client communication (Private message (fail)). Exiting thread.");
                    }

                }

            }
            else if (s.StartsWith("/Global"))
            {
                s = s.Remove(0, 8);

                // Decrypt the message using the server's private key;
                string decryptedText = secureChat.RSADecrypt(s);

                for (int i = 0; i < threadList.Count; i++)
                {
                    // Encrypt the message (including the user's name) using each client's public key;
                    string encyptedText = secureChat.RSAEncrypt(user.nick + ": " + decryptedText, threadList[i].RSAParams);

                    try
                    {
                        SendMessage(threadList[i].writer, "/Global " + encyptedText);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Problem with client communication. Exiting thread.");
                    }

                }
            }
            // Check for "/" commands, process them and send info back to just that client instead of echoing it to all clients;
            else if (s.StartsWith("/"))
            {
                string temp = CheckForSlashCommands(s, user);
                if (!String.IsNullOrWhiteSpace(temp))
                {
                    try
                    {
                        SendMessage(user.writer, temp);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Problem with client communication (Slash command). Exiting thread.");
                    }
                }
                else { Console.WriteLine("invalid '/' command."); }
            }
        }

        private void ProcessClientEndOfCommunications(ClientInfo user)
        {
            accounts[user.email].online = false;
            // Only remove disconnected client's thread from threadList if server is still listening to incoming traffic (i.e. not shutting down);

                for (int i = 0; i < threadList.Count; i++)
                {
                    if (threadList[i] == user)
                    {
                        threadList.Remove(threadList[i]);
                        break;
                    }
                }
        }


        public void SendMessage(StreamWriter writer, string message)
        {
            writer.WriteLine(message);
            writer.Flush();
        }


        public void StartListening()
        {
            try
            {
                listener = new TcpListenerWrapper(IPAddress.Any, 8080);
                listener.Start();
                Console.WriteLine("Chat server started...");
                Console.WriteLine("Waiting for incoming client connections...");
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread t = new Thread(ProcessClientRequests);
                    t.IsBackground = true;
                    t.Start(client);
                }
            }
            catch (SocketException e)
            {
                if (!listener.Active) { Console.WriteLine("TCP listener of Servercomms stopped listening."); }
                else { Console.WriteLine(e); }
            }
            //catch (Exception e) { Console.WriteLine(e); }
            finally { if (listener != null) listener.Stop(); }
        }


        public void StopListening()
        {
            listener.Stop();
            listening = false;
        }
    }
}
