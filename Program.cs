using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;

using CommandLine;

using DbZip.Tasks;
using DbZip.Threading;

using NLog;


namespace DbZip
{

	class Program
	{

		static void Main(string[] args)
		{
			//Set base-priority of the process so it hopefully doesn't interfere too much with SQL Server
			Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;


			//Parse command-line options
			var options = new CommandLineOptions();
			var parser = new CommandLineParser(new CommandLineParserSettings(false, true, Console.Error));
			if (!parser.ParseArguments(args, options)) {
				Environment.Exit(1);
			}
			if (String.IsNullOrEmpty(options.UserID) && String.IsNullOrEmpty(options.Password)) {
				options.IntegratedSecurity = true;
			}


			//Build SQL Server connection-string from command-line options
			var builder = new SqlConnectionStringBuilder {
				DataSource = options.DataSource ?? "localhost",
				UserID = options.UserID ?? "",
				Password = options.Password ?? "",
				IntegratedSecurity = options.IntegratedSecurity,
				InitialCatalog = options.InitialCatalog
			};


			var timer = new Stopwatch();
			try {
				//BACKUP DATABASE
				string backupFileName;
				using (new GlobalMutex()) {
					Log.Info("Backing up: [{0}].[{1}]", builder.DataSource, builder.InitialCatalog);
					timer.Start();

					var backupTask = new BackupTask(
						builder.ConnectionString, 
						builder.InitialCatalog, 
						options.TransactionLogBackup, 
						statementTimeout: (int)TimeSpan.FromHours(4).TotalSeconds
					);

					if (Log.IsDebugEnabled) {
						backupTask.Progress += (sender, eventArgs) => Log.Debug(eventArgs.Message);
					}

					backupFileName = backupTask.Run();

					timer.Stop();
					Log.Info("Backed up in {0} ms", timer.ElapsedMilliseconds);
				}


				//ARCHIVE DATABASE BACKUP
				Log.Info("Zipping up: [{0}]", backupFileName);
				timer.Restart();
				string archiveFileName = new ZipTask(backupFileName).Run();
				timer.Stop();
				Log.Info("Zipped up in {0} ms", timer.ElapsedMilliseconds);


				//VERIFY ARCHIVE AND CLEANUP
				Log.Info("Verifying: [{0}]", archiveFileName);
				bool isValid = ZipTask.Verify(archiveFileName);
				Log.Info("Verification {0} in {1} ms", isValid ? "passed" : "failed", timer.ElapsedMilliseconds);
				if (isValid) {
					if (File.Exists(backupFileName)) {
						Log.Info("Deleting {0}", backupFileName);
						File.Delete(backupFileName);
					}
				}


				Log.Info("Completed");
				Log.Info("-------------------------------------------------------------------------------");

			} catch (TimeoutException) {
				Log.Trace("A backup is already in progress");

			} catch (Exception ex) {
				Log.Error(ex.Message, ex);
			}

		}


		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	}

}
