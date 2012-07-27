using System;

using CommandLine;
using CommandLine.Text;


namespace DbZip
{

	public class CommandLineOptions : CommandLineOptionsBase
	{

		[Option("S", "Server", Required = false, HelpText = "Sets the name or network address of the instance of SQL Server to connect to.")]
		public string Server { get; set; }


		[Option("D", "Database", Required = true, HelpText = "Sets the name of the database associated with the connection.")]
		public string Database { get; set; }


		[Option("E", "IntegratedSecurity", MutuallyExclusiveSet = "windowsAuth", HelpText = "Indicates to use the current Windows account credentials for authentication instead of UserID and Password.")]
		public bool IntegratedSecurity { get; set; }


		[Option("U", "UserID", MutuallyExclusiveSet = "manualAuth", HelpText = "Sets the user ID to be used when connecting to SQL Server.")]
		public string UserID { get; set; }


		[Option("P", "Password", MutuallyExclusiveSet = "manualAuth", HelpText = "Sets the password to be used when connecting to SQL Server.")]
		public string Password { get; set; }


		[Option("T", "transactionLog", DefaultValue = false, HelpText = "By default DbZip does a full database backup, however if this option is supplied it will do a transaction-log backup instead")]
		public bool TransactionLogBackup { get; set; }


		[HelpOption]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
		}

	}

}
