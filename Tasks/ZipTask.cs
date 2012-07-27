using System;
using System.Diagnostics.Contracts;

using Ionic.Zip;
using Ionic.Zlib;


namespace DbZip.Tasks
{

	public class ZipTask
	{

		public static string Zip(string fileName, CompressionLevel compressionLevel = CompressionLevel.Default)
		{
			return new ZipTask(fileName, compressionLevel).Run();
		}


		public static bool Verify(string zipFileName)
		{
			return ZipFile.IsZipFile(zipFileName, true);
		}


		public ZipTask(string fileName, CompressionLevel compressionLevel = CompressionLevel.Default)
		{
			Contract.Requires(!String.IsNullOrEmpty(_fileName));

			_fileName = fileName;
			_compressionLevel = compressionLevel;
		}


		public string Run()
		{
			string zipFileName = _fileName + ".zip";
			using (var zip = new ZipFile()) {
				zip.UseZip64WhenSaving = Zip64Option.Always;
				zip.CompressionLevel = _compressionLevel;
				zip.AddFile(_fileName, "");
				zip.Save(zipFileName);
			}
			return zipFileName;
		}


		private readonly string _fileName;
		private readonly CompressionLevel _compressionLevel;

	}

}
