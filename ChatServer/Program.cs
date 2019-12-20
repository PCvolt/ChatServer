using System;
using System.Net;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Data.SQLite;
using System.Text.Json;
using System.Linq;

public enum MsgType
{
	createprofile	= 0,
	login			= 1,
	listtopics		= 2,
	createtopic		= 3,
	jointopic		= 4,
	sendmsg			= 5,
	sendprivmsg		= 6,
	help			= 7
}


public struct Message
{
	public MsgType Mymsgtype { get; set; }
	public string S1 { get; set; }
	public string S2 { get; set; }

	public Message(MsgType msgtype, string s1, string s2)
	{
		this.Mymsgtype = msgtype;
		this.S1 = s1;
		this.S2 = s2;
	}
}

public struct Client
{
	public TcpClient s;
	public String username;
	public String topic;

	public Client(TcpClient client, String usn, String tpc = "#welcome")
	{
		this.s = client;
		this.username = usn;
		this.topic = tpc;
	}
};

struct Profile
{
	String username;
	String password;

	public Profile(String usn, String pass)
	{
		this.username = usn;
		this.password = pass;
	}

	public string Username { get { return username; } set { username = value; } }
	public string Password { get { return password; } set { password = value; } }
};


class Program
{
	/* DATABASE */
	static void profilesFromDB(SQLiteConnection conn, List<Profile> profiles)
	{
		conn.Open();
		SQLiteDataReader sqReader;
		SQLiteCommand sqlite_cmd;
		sqlite_cmd = conn.CreateCommand();
		sqlite_cmd.CommandText = "SELECT * FROM profiles;";

		sqReader = sqlite_cmd.ExecuteReader();
		while (sqReader.Read())
			profiles.Add(new Profile(sqReader.GetString(sqReader.GetOrdinal("username")), sqReader.GetString(sqReader.GetOrdinal("password"))));
		
		conn.Close();
	}

	static void insertProfileInDB(SQLiteConnection conn, Profile profile, List<Profile> profiles)
	{
		conn.Open();
		SQLiteCommand sqlite_cmd = new SQLiteCommand("INSERT INTO profiles (username, password) VALUES (@username, @password);", conn);
		sqlite_cmd.Parameters.AddWithValue("@username", profile.Username);
		sqlite_cmd.Parameters.AddWithValue("@password", profile.Password);
		try
		{
			sqlite_cmd.ExecuteNonQuery();
		}
		catch (Exception ex) { throw new Exception(ex.Message); }

		conn.Close();
	}

	/* MESSAGE HANDLING */
	static Message receiveMessage(TcpClient s)
	{
		NetworkStream ns = s.GetStream();
		byte[] bytesFrom = new byte[1024];
		ns.Read(bytesFrom, 0, bytesFrom.Length);
		Utf8JsonReader utf8Reader = new Utf8JsonReader(bytesFrom);

		return JsonSerializer.Deserialize<Message>(ref utf8Reader);
	}

	static void sendMessage(NetworkStream ns, Message myMessage)
	{
		Byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(myMessage);

		ns.Write(jsonUtf8Bytes, 0, jsonUtf8Bytes.Length);
	}

	static void writeToSingle(Client client, Message myMessage)
	{
		NetworkStream ns = client.s.GetStream();
		sendMessage(ns, myMessage);
	}

	static void writeToEveryone(List<Client> clients, Message myMessage)
	{
		foreach (Client client in clients)
		{
			writeToSingle(client, myMessage);
		}
	}


	static void Main(string[] args)
	{
		List<Client> clients = new List<Client>();
		List<Profile> profiles = new List<Profile>();
		Dictionary<String, List<Client>> topics = new Dictionary<string, List<Client>>();

		/* INIT LIST FROM DATABASE */
		SQLiteConnection conn = new SQLiteConnection("Data Source=irc_users.sqlite;Version=3;");
		profilesFromDB(conn, profiles);

		/* INIT TCP SOCKET */
		TcpListener serverSocket = new TcpListener(new IPAddress(new byte[] { 127, 0, 0, 1 }), 8888);
		serverSocket.Start();
		Console.WriteLine("SERVER UP");

		do
		{
			TcpClient clientSocket = default(TcpClient);
			clientSocket = serverSocket.AcceptTcpClient();
			Client c = new Client(clientSocket, "User #" + clients.Count);
			clients.Add(c);

			
			topics["#welcome"] = clients;



			Console.WriteLine(c.username + " enters the room");
			writeToEveryone(clients, new Message(MsgType.sendmsg, ">> " + c.username + " enters the room.", null));
			
			//send to everyone list of topics
			// Launch a new thread per new socket
			new Thread(() =>
			{
				try
				{
					//topics.Keys.ToArray();
					while (true)
					{
						// Receive msgtype from the client
						Message myMessage = receiveMessage(clientSocket);

						Console.WriteLine(myMessage.Mymsgtype);
						switch (myMessage.Mymsgtype)
						{
							case MsgType.createprofile:
								writeToSingle(c, new Message(MsgType.sendmsg, "Creating profile...", null));
								
								string new_username = myMessage.S1;
								string new_password = myMessage.S2;

								//Check
								bool alreadyExists = false;
								foreach (Profile p in profiles)
								{
									if (new_username.Equals(p.Username))
									{
										alreadyExists = true;
										break;
									}
								}
		
								if (!alreadyExists)
								{
									Profile new_profile = new Profile(new_username, new_password);
									profiles.Add(new_profile);
									insertProfileInDB(conn, new_profile, profiles);
									writeToSingle(c, new Message(MsgType.sendmsg, new_username + " has been created in the database!", null));
								}
								else
								{
									writeToSingle(c, new Message(MsgType.sendmsg, "This username has already been taken!", null));
								}
								
								break;

							case MsgType.login:
								string log_username = myMessage.S1;
								string log_password = myMessage.S2;

								bool success = false;
								foreach (Profile profile in profiles)
								{
									if (log_username.Equals(profile.Username) && log_password.Equals(profile.Password))
									{
										writeToSingle(c, new Message(MsgType.sendmsg, Environment.NewLine + "Logged as " + profile.Username, null));
										c.username = log_username;
										success = true;
										break;
									}
								}
								if (!success)
									writeToSingle(c, new Message(MsgType.sendmsg, "Failed to log in.", null));
								break;

							case MsgType.createtopic:
								
								/*
								if (typeFromClient != MsgType.sendmsg)
								{
									string firstWord = dataFromClient.IndexOf(" ") > -1 ? dataFromClient.Substring(0, dataFromClient.IndexOf(" ")) : dataFromClient;

									int index = dataFromClient.IndexOf(firstWord);
									dataFromClient = (index < 0) ? dataFromClient : dataFromClient.Remove(index, firstWord.Length);
								}
								*/
								break;

							case MsgType.jointopic:
								Console.WriteLine("User X joins topic Y.");
								break;

							case MsgType.sendprivmsg:
								break;

							case MsgType.sendmsg: // Send message back to all clients
												  // Receive message from the client
								Message answer = new Message(MsgType.sendmsg, null, null);
								answer.S1 = "[" + DateTime.Now.ToString("hh:mm:ss") + "] " + c.username + ": " + myMessage.S1;

								Console.WriteLine(answer.S1);
								writeToEveryone(clients, answer);
								break;

							case MsgType.help:
								writeToSingle(c, new Message(MsgType.sendmsg, Environment.NewLine + Environment.NewLine + "/createprofile <username> <password>" + Environment.NewLine + "/login <username> <password>" + Environment.NewLine + "/createtopic <topic> (admin only)" + Environment.NewLine + "/listtopics (should be on the left bar but ok)"+ Environment.NewLine + "/jointopic <topic>" + Environment.NewLine + "/sendprivmsg <nickname>" + Environment.NewLine + "Send a regular message in the current topic." + Environment.NewLine + Environment.NewLine, null));
								break;
						}	
					}
				}
				catch (IOException ignore) { }
				finally
				{
					clientSocket.Close();
					clients.Remove(c);
					writeToEveryone(clients, new Message(MsgType.sendmsg, c.username + " left the room.", null));
				}

			}).Start();
		} while (clients.Count > 0);

		serverSocket.Stop();
		Console.WriteLine(" >> exit");
		Console.ReadLine();
	}
}
// http://csharp.net-informations.com/communications/csharp-server-socket.htm
// http://csharp.net-informations.com/communications/csharp-client-socket.htm
// https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient?redirectedfrom=MSDN&view=netframework-4.8

/*
// server:
public class ChatServer
{

	public static int Main(String[] args)
	{
		while (true)
		{
			//Start thread
			try
			{
				while (true)
				{
					MsgType messageType = (MsgType)clientStreamReader.Read();
					switch (messageType)
					{
						
						case MsgType.listtopics:
							clientStreamWriter.Write(MsgType.listtopics);
							clientStreamWriter.Write(topics.Count);
							foreach (String t in topics)
								clientStreamWriter.WriteLine(t); //quand ya pas "s" (client), à qui/où j'écris ça
							break;

						case MsgType.createtopic:
							String topic = clientStreamReader.ReadLine();
							topics[topic] = []; //very JS, jsp comment transcrire
							break;

						case MsgType.jointopic:
							String topic = clientStreamReader.ReadLine();
							if (false) //si topics a pas topic
								return 1;
							topics[topic] += c;
							c.topic = topic;
							break;
						case MsgType.sendprivmsg:
							// laissé en exercice au lecteur ^^
							break;

						default:
							break;
					}
				}
			}
			finally
			{
				s.Close();
				clients.Remove(c);
				foreach (clients in topics)
					removeall(c);
			}
		}
	}
}*/
