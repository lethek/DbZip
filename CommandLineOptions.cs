using System;

using CommandLine;
using CommandLine.Text;


namespace DbZip
{

	public class CommandLineOptions : CommandLineOptionsBase
	{

		[Option("D", "Database", Required = true, HelpText = "Sets the name of the database associated with the connection.")]
		public string Database { get; set; }

		[Option("S", "Server", DefaultValue = "localhost", HelpText = "Sets the name or network address of the instance of SQL Server to connect to. Defaults to localhost.")]
		public string Server { get; set; }

		[Option("U", "UserID", DefaultValue = "", HelpText = "Sets the user ID to be used when connecting to SQL Server. If no user ID is supplied then current Windows account credentials are used.")]
		public string UserID { get; set; }

		[Option("P", "Password", DefaultValue = "", HelpText = "Sets the password to be used when connecting to SQL Server. If no password is supplied then current Windows account credentials are used.")]
		public string Password { get; set; }

		[Option("T", "TransactionLog", DefaultValue = false, HelpText = "By default DbZip does a full database backup, however if this option is supplied it will do a transaction-log backup instead.")]
		public bool TransactionLogBackup { get; set; }

		[Option("W", "Wait", DefaultValue = false, HelpText = "Tells DbZip that if another backup is already in progress, it should wait until that completes before running this backup. Default behaviour is to skip this backup and exit immediately.")]
		public bool Wait { get; set; }


		[HelpOption]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
		}

	}

}
