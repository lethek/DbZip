using System;


namespace DbZip
{

	public class Options
	{

		public string Database { get; set; }

		public string Server { get; set; }

		public string User { get; set; }

		public string Password { get; set; }

		public bool TransactionLogBackup { get; set; }

		public bool Wait { get; set; }

		public bool SevenZip { get; set; }

		public bool UseIntegratedSecurity { get { return String.IsNullOrEmpty(User) || String.IsNullOrEmpty(Password); } }

	}

}
