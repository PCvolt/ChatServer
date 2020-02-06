using System;
using System.Net.Sockets;


namespace ChatServer
{
	struct Client
	{
		public TcpClient s;
		public string username;
		public string topic;

		public Client(TcpClient client, string usn, string tpc)
		{
			this.s = client;
			this.username = usn;
			this.topic = tpc;
		}
	};
}
