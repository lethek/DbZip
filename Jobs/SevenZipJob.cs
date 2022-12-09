using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using Serilog;
using Serilog.Events;

using SevenZip;


namespace DbZip.Jobs
{

    public class SevenZipJob : ICompressionJob
    {

        public string FileName { get; }
        public string ZipFileName { get; }
        public CompressionLevel CompressionLevel { get; }


        public SevenZipJob(string fileName)
            : this(fileName, CompressionLevel.Low)
        {
        }


        public SevenZipJob(string fileName, CompressionLevel compressionLevel)
        {
            FileName = fileName;
            ZipFileName = FileName + ".7z";
            CompressionLevel = compressionLevel;

            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Environment.Is64BitProcess ? "x64" : "x86", "7z.dll");
            SevenZipBase.SetLibraryPath(path);
        }


        public void Compress()
        {
            Log.Information("Zipping up: {ZipFileName}", ZipFileName);

            var compressor = new SevenZipCompressor {
                CompressionMethod = CompressionMethod.Lzma2,
                CompressionLevel = CompressionLevel,
                ArchiveFormat = OutArchiveFormat.SevenZip,
                EventSynchronization = EventSynchronizationStrategy.AlwaysAsynchronous,
                FastCompression = false
            };
            compressor.CustomParameters.Add("mt", "on");

            if (Log.IsEnabled(LogEventLevel.Verbose)) {
                compressor.Compressing += (sender, args) => { Log.Verbose("{PercentDone} percent processed.", args.PercentDone); };
            }

            var timer = Stopwatch.StartNew();
            compressor.CompressFiles(ZipFileName, FileName);
            Log.Information("Zipped up in {ElapsedMilliseconds} ms", timer.ElapsedMilliseconds);
        }


        public bool Verify()
        {
            Log.Information("Verifying: {0}", ZipFileName);
            var extractor = new SevenZipExtractor(ZipFileName);

            var timer = Stopwatch.StartNew();
            bool isValid = extractor.Check();
            Log.Information("Verification {VerificationStatus} in {ElapsedMilliseconds} ms", isValid ? "passed" : "failed", timer.ElapsedMilliseconds);
            return isValid;
        }

    }
}
