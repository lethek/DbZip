using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Threading;

using DbZip.Jobs;
using DbZip.Threading;

using Fclp;

using Serilog;
using Serilog.Events;


namespace DbZip
{

    public class Program
    {

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(LogEventLevel.Information, "{Message}{NewLine}")
                .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Logs\Log.{Date}.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            int exitCode = SystemErrorCodes.SUCCESS;

            try {
                //Set base-priority of the process so it hopefully doesn't interfere too much with SQL Server
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

                Log.Verbose("Command: {0}", Environment.CommandLine);
                var parser = new FluentCommandLineParser<Options> { IsCaseSensitive = false };
                ConfigureCommandLineOptions(parser);
                var parserResult = parser.Parse(args);

                if (parserResult.HasErrors) {
                    exitCode = SystemErrorCodes.ERROR_BAD_ARGUMENTS;
                    Log.Error(parserResult.ErrorText);
                } else if (parserResult.HelpCalled) {
                    exitCode = SystemErrorCodes.SUCCESS;
                } else {
                    Start(parser.Object);
                }
            } catch (Exception ex) {
                Log.Error(ex.Message, ex);
                exitCode = SystemErrorCodes.ERROR_UNKNOWN;
            } finally {
                Log.Debug("-------------------------------------------------------------------------------");
            }

            //Exit ensuring no foreground threads stay running
            Environment.Exit(exitCode);
        }


        private static void Start(Options options)
        {
            //Build SQL Server connection-string from command-line options
            var builder = new SqlConnectionStringBuilder { DataSource = options.Server };
            if (options.UseIntegratedSecurity) {
                builder.IntegratedSecurity = true;
            } else {
                builder.UserID = options.User;
                builder.Password = options.Password;
            }

            try {
                //BACKUP DATABASE
                string backupFileName;
                using (new GlobalMutex(timeout: (options.Wait ? Timeout.Infinite : 0))) {
                    Log.Information(
                        "Backing up: [{0}].[{1}] ({2})", builder.DataSource, options.Database,
                        options.TransactionLogBackup ? "TRANSACTION-LOG" : "FULL"
                    );
                    var timer = Stopwatch.StartNew();

                    var backupTask = new BackupJob(
                        builder.ConnectionString,
                        options.Database,
                        options.TransactionLogBackup,
                        statementTimeout: (int)TimeSpan.FromHours(4).TotalSeconds
                    );

                    if (Log.IsEnabled(LogEventLevel.Debug)) {
                        backupTask.Progress += (sender, eventArgs) => Log.Debug(eventArgs.Message);
                    }

                    backupFileName = backupTask.Run();

                    Log.Information("Backed up in {0} ms", timer.ElapsedMilliseconds);
                }


                var compressionJob = options.SevenZip
                    ? (ICompressionJob)new SevenZipJob(backupFileName)
                    : (ICompressionJob)new ZipJob(backupFileName);

                //ARCHIVE DATABASE BACKUP
                compressionJob.Compress();

                //VERIFY ARCHIVE AND CLEANUP
                if (compressionJob.Verify()) {
                    if (File.Exists(backupFileName)) {
                        Log.Information("Deleting {0}", backupFileName);
                        File.Delete(backupFileName);
                    }
                }


                Log.Information("Completed");
            } catch (TimeoutException) {
                const string message = "Another backup is already in progress";
                Log.Verbose(message);
                Console.Error.WriteLine(message);
            }
        }


        private static void ConfigureCommandLineOptions(FluentCommandLineParser<Options> parser)
        {
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
                .WithDescription(
                    "Sets the user ID to be used when connecting to SQL Server. If no user ID is supplied then current Windows account credentials are used."
                );

            parser
                .Setup(o => o.Password)
                .As('p', "Password")
                .WithDescription(
                    "Sets the password to be used when connecting to SQL Server. If no password is supplied then current Windows account credentials are used."
                );

            parser
                .Setup(o => o.TransactionLogBackup)
                .As('t', "TransactionLog")
                .WithDescription(
                    "By default DbZip does a full database backup, however if this option is supplied it will do a transaction-log backup instead."
                )
                .SetDefault(false);

            parser
                .Setup(o => o.Wait)
                .As('w', "Wait")
                .WithDescription(
                    "Tells DbZip that if another backup is already in progress, it should wait until that completes before running this backup. Default behaviour is to skip this backup and exit immediately."
                )
                .SetDefault(false);

            parser
                .Setup(o => o.SevenZip)
                .As('7', "SevenZip")
                .WithDescription("Uses 7-zip to compress the database backup instead of Zip.")
                .SetDefault(false);

            parser.SetupHelp("h", "help", "?")
                .WithCustomFormatter(
                    new TidyCommandLineOptionFormatter { Usage = "Usage: DbZip -D databaseName [-S serverAddress] [-U userID] [-P password]" }
                )
                .UseForEmptyArgs()
                .Callback(x => Console.WriteLine(x));
        }

    }

}
