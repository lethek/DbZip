using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using DbZip.Tasks;
using DbZip.Threading;

using Fclp;
using Fclp.Internals;

using Serilog;
using Serilog.Events;

using SevenZip;


namespace DbZip
{

	class Program
	{

		public static void Main(string[] args)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.WriteTo.ColoredConsole(LogEventLevel.Information, "{Message}{NewLine}")
				.WriteTo.RollingFile(@"Logs\Log.{Date}.txt")
				.CreateLogger();

			try {
				//Set base-priority of the process so it hopefully doesn't interfere too much with SQL Server
				Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

				var parser = new FluentCommandLineParser<Options> { IsCaseSensitive = false };

				parser
					.Setup(o => o.Database)
					.As('d', "Database")
					.WithDescription("Sets the name of the database associated with the connection.")
					.Required();

				parser
					.Setup(o => o.Server)
					.As('s', "Server")
					.WithDescription("Sets the name or network address of the instance of SQL Server to connect to. Defaults to localhost.")
					.SetDefault("localhost");

				parser
					.Setup(o => o.User)
					.As('u', "User")
					.WithDescription("Sets the user ID to be used when connecting to SQL Server. If no user ID is supplied then current Windows account credentials are used.");

				parser
					.Setup(o => o.Password)
					.As('p', "Password")
					.WithDescription("Sets the password to be used when connecting to SQL Server. If no password is supplied then current Windows account credentials are used.");

				parser
					.Setup(o => o.TransactionLogBackup)
					.As('t', "TransactionLog")
					.WithDescription("By default DbZip does a full database backup, however if this option is supplied it will do a transaction-log backup instead.")
					.SetDefault(false);

				parser
					.Setup(o => o.Wait)
					.As('w', "Wait")
					.WithDescription("Tells DbZip that if another backup is already in progress, it should wait until that completes before running this backup. Default behaviour is to skip this backup and exit immediately.")
					.SetDefault(false);

				parser
					.Setup(o => o.SevenZip)
					.As('7', "SevenZip")
					.WithDescription("Uses 7-zip to compress the database backup instead of Zip.")
					.SetDefault(false);

				parser.SetupHelp("h", "help", "?").WithCustomFormatter(new TidyCommandLineOptionFormatter {
					Usage = "Usage: DbZip -D databaseName [-S serverAddress] [-U userID] [-P password]"
				})
					.UseForEmptyArgs()
					.Callback(x => Console.WriteLine(x));

				var result = parser.Parse(args);

				if (result.HasErrors) {
					Console.WriteLine(result.ErrorText);
					Environment.Exit(ERROR_BAD_ARGUMENTS);
				}

				if (result.HelpCalled) {
					Environment.Exit(0);
				}

				Execute(parser.Object);

			} catch (Exception ex) {
				Log.Error(ex.Message, ex);
				Console.Error.WriteLine(ex.Message);
				Environment.Exit(-1);
			}

			//Exit ensuring no foreground threads stay running
			Environment.Exit(0);
		}


		private static void Execute(Options options)
		{
			//Build SQL Server connection-string from command-line options
			bool useIntegratedSecurity = String.IsNullOrEmpty(options.User) || String.IsNullOrEmpty(options.Password);
			var builder = new SqlConnectionStringBuilder {
				DataSource = options.Server,
				IntegratedSecurity = useIntegratedSecurity,
				UserID = useIntegratedSecurity ? "" : options.User,
				Password = useIntegratedSecurity ? "" : options.Password
			};


			var timer = new Stopwatch();
			try {
				//BACKUP DATABASE
				string backupFileName;
				using (new GlobalMutex(timeout: (options.Wait ? Timeout.Infinite : 0))) {
					Log.Information("Backing up: [{0}].[{1}] ({2})", builder.DataSource, options.Database, options.TransactionLogBackup ? "TRANSACTION-LOG" : "FULL");
					timer.Start();

					var backupTask = new BackupTask(
						builder.ConnectionString,
						options.Database,
						options.TransactionLogBackup,
						statementTimeout: (int)TimeSpan.FromHours(4).TotalSeconds
					);

					if (Log.IsEnabled(LogEventLevel.Debug)) {
						backupTask.Progress += (sender, eventArgs) => Log.Debug(eventArgs.Message);
					}

					backupFileName = backupTask.Run();

					timer.Stop();
					Log.Information("Backed up in {0} ms", timer.ElapsedMilliseconds);
				}


				if (options.SevenZip) {
					//ARCHIVE DATABASE BACKUP
					Log.Information("Zipping up: {0}", backupFileName);
					var task = new SevenZipTask(backupFileName, CompressionLevel.Low);
					if (Log.IsEnabled(LogEventLevel.Verbose)) {
						task.Progress += (sender, args) => { Log.Verbose(args.PercentDone + " percent processed."); };
					}

					timer.Restart();
					string archiveFileName = task.Run();
					timer.Stop();
					Log.Information("Zipped up in {0} ms", timer.ElapsedMilliseconds);

					//VERIFY ARCHIVE AND CLEANUP
					Log.Information("Verifying: {0}", archiveFileName);
					timer.Restart();
					bool isValid = SevenZipTask.Verify(archiveFileName);
					timer.Stop();
					Log.Information("Verification {0} in {1} ms", isValid ? "passed" : "failed", timer.ElapsedMilliseconds);
					if (isValid) {
						if (File.Exists(backupFileName)) {
							Log.Information("Deleting {0}", backupFileName);
							File.Delete(backupFileName);
						}
					}

				} else {
					//ARCHIVE DATABASE BACKUP
					Log.Information("Zipping up: {0}", backupFileName);
					timer.Restart();
					string archiveFileName = new ZipTask(backupFileName).Run();
					timer.Stop();
					Log.Information("Zipped up in {0} ms", timer.ElapsedMilliseconds);

					//VERIFY ARCHIVE AND CLEANUP
					Log.Information("Verifying: {0}", archiveFileName);
					timer.Restart();
					bool isValid = ZipTask.Verify(archiveFileName);
					timer.Stop();
					Log.Information("Verification {0} in {1} ms", isValid ? "passed" : "failed", timer.ElapsedMilliseconds);
					if (isValid) {
						if (File.Exists(backupFileName)) {
							Log.Information("Deleting {0}", backupFileName);
							File.Delete(backupFileName);
						}
					}
				}


				Log.Information("Completed");
				Log.Debug("-------------------------------------------------------------------------------");

			} catch (TimeoutException) {
				const string message = "Another backup is already in progress";
				Log.Verbose(message);
				Console.Error.WriteLine(message);
			}
		}


		private const int ERROR_BAD_ARGUMENTS = 160;

	}

}
