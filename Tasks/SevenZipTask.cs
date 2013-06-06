using System;
using System.Diagnostics.Contracts;

using SevenZip;
using SevenZip.Compression;


namespace DbZip.Tasks
{

	public class SevenZipTask
	{

		public static string Zip(string fileName, CompressionLevel compressionLevel = CompressionLevel.Normal)
		{
			return new SevenZipTask(fileName, compressionLevel).Run();
		}


		public static bool Verify(string zipFileName)
		{
			var extractor = new SevenZipExtractor(zipFileName);
			return extractor.Check();
		}


		public SevenZipTask(string fileName, CompressionLevel compressionLevel = CompressionLevel.Normal)
		{
			Contract.Requires(!String.IsNullOrEmpty(_fileName));

			_fileName = fileName;
			_compressionLevel = compressionLevel;
		}


		public string Run()
		{
			string zipFileName = _fileName + ".7z";

			var compressor = new SevenZipCompressor {
				CompressionMethod = CompressionMethod.Lzma2,
				CompressionLevel = _compressionLevel,
				ArchiveFormat = OutArchiveFormat.SevenZip,
				EventSynchronization = EventSynchronizationStrategy.AlwaysSynchronous,
				FastCompression = true
			};
			compressor.CompressFiles(zipFileName, _fileName);

			return zipFileName;
		}


		private readonly string _fileName;
		private readonly CompressionLevel _compressionLevel;

	}

}
