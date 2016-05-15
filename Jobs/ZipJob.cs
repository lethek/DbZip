using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

using Ionic.Zip;
using Ionic.Zlib;

using Serilog;


namespace DbZip.Jobs
{

	public class ZipJob : ICompressionJob
	{

		public string FileName { get; }
		public string ZipFileName { get; }
		public CompressionLevel CompressionLevel { get; }


		public ZipJob(string fileName) : this(fileName, CompressionLevel.Default)
		{
		}


		public ZipJob(string fileName, CompressionLevel compressionLevel)
		{
			Contract.Requires(!String.IsNullOrEmpty(fileName));

			FileName = fileName;
			ZipFileName = FileName + ".zip";
			CompressionLevel = compressionLevel;
		}


		public void Compress()
		{
			Log.Information($"Zipping up: {ZipFileName}");
			var timer = Stopwatch.StartNew();

			using (var zip = new ZipFile()) {
				zip.UseZip64WhenSaving = Zip64Option.Always;
				zip.CompressionLevel = CompressionLevel;
				zip.AddFile(FileName, "");
				zip.Save(ZipFileName);
			}

			Log.Information($"Zipped up in {timer.ElapsedMilliseconds} ms");
		}


		public bool Verify()
		{
			Log.Information($"Verifying: {ZipFileName}");
			var timer = Stopwatch.StartNew();
			bool isValid = ZipFile.IsZipFile(ZipFileName, true);
			Log.Information("Verification {0} in {1} ms", isValid ? "passed" : "failed", timer.ElapsedMilliseconds);
			return isValid;
		}

	}

}
