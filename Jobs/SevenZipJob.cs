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

		public string FileName { get; private set; }
		public string ZipFileName { get; private set; }
		public CompressionLevel CompressionLevel { get; private set; }


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
			Log.Information("Zipping up: {0}", FileName);

			var compressor = new SevenZipCompressor {
				CompressionMethod = CompressionMethod.Lzma2,
				CompressionLevel = CompressionLevel,
				ArchiveFormat = OutArchiveFormat.SevenZip,
				EventSynchronization = EventSynchronizationStrategy.AlwaysSynchronous,
				//FastCompression = true
			};

			if (Log.IsEnabled(LogEventLevel.Verbose)) {
				compressor.Compressing += (sender, args) => { Log.Verbose(args.PercentDone + " percent processed."); };
			}

			var timer = Stopwatch.StartNew();
			compressor.CompressFiles(ZipFileName, FileName);
			timer.Stop();
			Log.Information("Zipped up in {0} ms", timer.ElapsedMilliseconds);
		}


		public bool Verify()
		{
			Log.Information("Verifying: {0}", ZipFileName);
			var extractor = new SevenZipExtractor(ZipFileName);

			var timer = Stopwatch.StartNew();
			bool isValid = extractor.Check();
			timer.Stop();
			
			Log.Information("Verification {0} in {1} ms", isValid ? "passed" : "failed", timer.ElapsedMilliseconds);

			return isValid;
		}

	}
}
