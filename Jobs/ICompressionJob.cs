namespace DbZip.Jobs
{

    public interface ICompressionJob
    {
        string FileName { get; }
        string ZipFileName { get; }
        void Compress();
        bool Verify();
    }

}
