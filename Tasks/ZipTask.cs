using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

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


		private string _fileName;
		private CompressionLevel _compressionLevel;

	}

}
