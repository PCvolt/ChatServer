using System.Collections.Generic;


namespace ChatServer
{
	enum MsgType
	{
		createprofile = 0,
		login = 1,
		listtopics = 2,
		createtopic = 3,
		listusers = 4,
		sendmsg = 5,
		sendprivmsg = 6,
		switchtopic = 7,
		help = 8
	}

	struct Message
	{
		public MsgType Mymsgtype { get; set; }
		public List<string> S { get; set; }
		public string Topic { get; set; }

		public Message(MsgType msgtype, List<string> listofString, string tpc = null)
		{
			this.Mymsgtype = msgtype;
			this.S = new List<string>(listofString);
			this.Topic = tpc;
		}
	}
}
