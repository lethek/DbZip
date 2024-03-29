﻿using System;
using System.IO;

using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;


namespace DbZip.Jobs
{

    public class BackupJob
    {

        public event ServerMessageEventHandler Complete;
        public event PercentCompleteEventHandler Progress;


        public static string Backup(string connectionString, string databaseName, bool transactionLogBackup = false, DateTime? expirationDate = null, int statementTimeout = 0)
            => new BackupJob(connectionString, databaseName, transactionLogBackup, expirationDate).Run();


        public BackupJob(string connectionString, string databaseName, bool transactionLogBackup = false, DateTime? expirationDate = null, int statementTimeout = 0)
        {
            _connectionString = connectionString;
            _databaseName = databaseName;
            _transactionLogBackup = transactionLogBackup;
            _expirationDate = expirationDate ?? DateTime.Now.AddDays(7);
            _statementTimeout = statementTimeout;
        }


        public string Run()
        {
            using (var sqlConnection = new SqlConnection(_connectionString)) {
                //Connect
                var connection = new ServerConnection(sqlConnection) { StatementTimeout = _statementTimeout };
                var server = new Server(connection);
                var database = server.Databases[_databaseName];

                //Validate
                if (database == null || database.IsSystemObject) {
                    throw new Exception($"Cannot find a non-system database named [{_databaseName}]");
                }
                if (_transactionLogBackup && database.DatabaseOptions.RecoveryModel == RecoveryModel.Simple) {
                    throw new Exception($"Cannot backup the transaction-logs because the [{_databaseName}] database is using the Simple recovery-model");
                }

                //Prepare backup options
                string backupExt = _transactionLogBackup ? "trn" : "bak";
                string backupFilename = $"{_databaseName}_backup_{DateTime.Now:yyyy_MM_dd_HHmmss}.{backupExt}";

                var backup = new Backup {
                    Action = _transactionLogBackup ? BackupActionType.Log : BackupActionType.Database,
                    BackupSetDescription = (_transactionLogBackup ? "Transaction log backup of " : "Full backup of ") + _databaseName,
                    BackupSetName = _databaseName + " Backup",
                    CompressionOption = BackupCompressionOptions.Off,
                    Database = _databaseName,
                    Devices = { new BackupDeviceItem(backupFilename, DeviceType.File) },
                    ExpirationDate = _expirationDate,
                    Incremental = false,
                    Initialize = true,
                    LogTruncation = BackupTruncateLogType.Truncate,
                    NoRecovery = false,
                };

                backup.Complete += Complete;
                backup.PercentComplete += Progress;

                //Backup
                backup.SqlBackup(server);

                return Path.Combine(server.Settings.BackupDirectory, backupFilename);
            }
        }


        private readonly string _connectionString;
        private readonly string _databaseName;
        private readonly bool _transactionLogBackup;
        private readonly DateTime _expirationDate;
        private readonly int _statementTimeout;

    }

}
