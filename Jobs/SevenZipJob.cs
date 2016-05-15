using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

using Serilog;
using Serilog.Events;

using SevenZip;


namespace DbZip.Jobs
{

	public class SevenZipJob: ICompressionJob
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
			Contract.Requires(!String.IsNullOrEmpty(FileName));

			FileName = fileName;
			ZipFileName = FileName + ".7z";
			CompressionLevel = compressionLevel;
		}


		public void Compress()
		{
			Log.Information($"Zipping up: {FileName}");

			var compressor = new SevenZipCompressor {
				CompressionMethod = CompressionMethod.Lzma2,
				CompressionLevel = CompressionLevel,
				ArchiveFormat = OutArchiveFormat.SevenZip,
				EventSynchronization = EventSynchronizationStrategy.AlwaysSynchronous,
				//FastCompression = true
			};

			if (Log.IsEnabled(LogEventLevel.Verbose)) {
				compressor.Compressing += (sender, args) => { Log.Verbose($"{args.PercentDone} percent processed."); };
			}

			var timer = Stopwatch.StartNew();
			compressor.CompressFiles(ZipFileName, FileName);
			Log.Information($"Zipped up in {timer.ElapsedMilliseconds} ms");
		}


		public bool Verify()
		{
			Log.Information($"Verifying: {ZipFileName}");
			var extractor = new SevenZipExtractor(ZipFileName);

			var timer = Stopwatch.StartNew();
			bool isValid = extractor.Check();
			Log.Information("Verification {0} in {1} ms", isValid ? "passed" : "failed", timer.ElapsedMilliseconds);
			return isValid;
		}

	}
}
