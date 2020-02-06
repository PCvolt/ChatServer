using System;


namespace ChatServer
{
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

}
