using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using CommandLine;
using DbZip.Tasks;
using DbZip.Threading;
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


				//Parse command-line options
				var parser = new Parser(settings => {
					settings.CaseSensitive = false;
					settings.HelpWriter = Console.Error;
				});
				var result = parser.ParseArguments<Options>(args);
				if (result.Errors.Any()) {
					Environment.Exit(ERROR_BAD_ARGUMENTS);
				}
				var options = result.Value;


				//Build SQL Server connection-string from command-line options
				bool useIntegratedSecurity = String.IsNullOrEmpty(options.UserID) || String.IsNullOrEmpty(options.Password);
				var builder = new SqlConnectionStringBuilder {
					DataSource = options.Server,
					IntegratedSecurity = useIntegratedSecurity,
					UserID = useIntegratedSecurity ? "" : options.UserID,
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
						timer.Restart();
						string archiveFileName = new SevenZipTask(backupFileName, CompressionLevel.Low).Run();
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

			} catch (Exception ex) {
				Log.Error(ex.Message, ex);
				Console.Error.WriteLine(ex.Message);
				Environment.Exit(-1);
			}

			//Exit ensuring no foreground threads stay running
			Environment.Exit(0);
		}


		private const int ERROR_BAD_ARGUMENTS = 160;

	}

}
