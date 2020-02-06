using ChatServer;
using System;
using System.Net;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Data.SQLite;
using System.Text.Json;
using System.Linq;



class Program
{
	#region Database
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
	#endregion

	#region Message handling
	static Message receiveMessage(TcpClient s)
	{
		NetworkStream ns = s.GetStream();
		byte[] bytesFrom = new byte[1024];
		ns.Read(bytesFrom, 0, bytesFrom.Length);
		Utf8JsonReader utf8Reader = new Utf8JsonReader(bytesFrom);
		Message myMessage = JsonSerializer.Deserialize<Message>(ref utf8Reader);

		return myMessage;
	}

	static void sendMessage(NetworkStream ns, Message myMessage)
	{
		Byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes(myMessage);

		ns.Write(jsonUtf8Bytes, 0, jsonUtf8Bytes.Length);
	}

	static void writeToSingle(Client client, Message myMessage)
	{
		NetworkStream ns = client.s.GetStream(); //fucks up if there the windows are not closed in order.
		sendMessage(ns, myMessage);
	}

	static void writeToEveryone(List<Client> clients, Message myMessage)
	{
		foreach (Client client in clients)
		{
			writeToSingle(client, myMessage);
		}
	}
	#endregion

	//static void writeToChannel(List<Client>)


	static void Main(string[] args)
	{
		List<Client> clients = new List<Client>();
		List<Profile> profiles = new List<Profile>();
		Dictionary<string, List<Client>> topics = new Dictionary<string, List<Client>>();

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
			Client c = new Client(clientSocket, "User #" + clients.Count, "#welcome");
			clients.Add(c);


			topics["#welcome"] = clients;

			Console.WriteLine(c.username + " enters the room");
			List<string> buffer = new List<string>();
			buffer.Add(">> " + c.username + " enters the room.");

			//send to everyone list of topics
			// Launch a new thread per new socket
			new Thread(() =>
			{
				try
				{
					writeToEveryone(clients, new Message(MsgType.listtopics, new List<string>(topics.Keys.ToArray())));
					Thread.Sleep(100); //Apparently needed for the message sending not to forget the latest client in the list.
					writeToEveryone(clients, new Message(MsgType.sendmsg, buffer, "#welcome"));
					Thread.Sleep(100); //Apparently needed for the message sending not to forget the latest client in the list.
					List<string> userlist = new List<string>();
					foreach (Client cl in clients)
						userlist.Add(cl.username);
					writeToEveryone(clients, new Message(MsgType.listusers, userlist));

					while (true)
					{
						// Receive msgtype from the client
						Message myMessage = receiveMessage(clientSocket);

						switch (myMessage.Mymsgtype)
						{
							case MsgType.createprofile:
								writeToSingle(c, new Message(MsgType.sendmsg, new List<string> { "Creating profile..." }));

								string new_username = myMessage.S[0];
								string new_password = myMessage.S[1];

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

									writeToSingle(c, new Message(MsgType.sendmsg, new List<string> { new_username + " has been created in the database!" }));
								}
								else
								{
									writeToSingle(c, new Message(MsgType.sendmsg, new List<string> { "This username has already been taken!" }));
								}

								break;

							case MsgType.login:
								string log_username = myMessage.S[0];
								string log_password = myMessage.S[1];

								bool success = false;
								foreach (Profile profile in profiles)
								{
									if (log_username.Equals(profile.Username) && log_password.Equals(profile.Password))
									{
										writeToSingle(c, new Message(MsgType.sendmsg, new List<string> { Environment.NewLine + "Logged as " + profile.Username }));

										//userlist[userlist.FindIndex(usn => usn.Equals(c.username))] = log_username;
										//c.username = log_username;

										writeToEveryone(clients, new Message(MsgType.listusers, userlist));
										success = true;
										break;
									}
								}
								if (!success)
								{
									writeToSingle(c, new Message(MsgType.sendmsg, new List<string> { "Failed to log in." }));
								}
								break;

							case MsgType.createtopic:

								topics.Add("#" + myMessage.S[0].Trim().Split(' ')[1], new List<Client> { c });

								writeToEveryone(clients, new Message(MsgType.listtopics, new List<string>(topics.Keys.ToArray())));
								break;

							case MsgType.sendprivmsg:
								break;

							case MsgType.sendmsg:
								Message answer = new Message(MsgType.sendmsg, new List<string> { "" }, c.topic);
								answer.S[0] = "[" + DateTime.Now.ToString("hh:mm:ss") + "] " + c.username + ": " + myMessage.S[0];

								Console.WriteLine(answer.S[0]);
								writeToEveryone(clients, answer);
								break;

							case MsgType.help:
								writeToSingle(c, new Message(MsgType.sendmsg, new List<string> { Environment.NewLine + "/createtopic <topic> (admin only)" + Environment.NewLine + "/jointopic <topic>" + Environment.NewLine + "/sendprivmsg <nickname>" + Environment.NewLine + Environment.NewLine }));
								break;
						}
					}
				}
				catch (IOException ignore) { }
				finally
				{
					clientSocket.Close();
					clients.Remove(c);
					writeToEveryone(clients, new Message(MsgType.sendmsg, new List<string> { c.username + " left the server." }));

					List<string> userlist = new List<string>();
					foreach (Client cl in clients)
						userlist.Add(cl.username);
					writeToEveryone(clients, new Message(MsgType.listusers, userlist));
				}

			}).Start();
		} while (clients.Count > 0);

		serverSocket.Stop();
		Console.WriteLine(" >> exit");
		Console.ReadLine();
	}
}

/*
 * cleaner buffer 
 * Doesn't send the list of channels when the client connects
*/
