namespace FinalProject_Store.Infrastructures.Storage
{
    public sealed class MinioOptions
    {
        public const string SectionName = "Minio";

        public string Endpoint { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = "store-files";
        public bool UseSSL { get; set; }
    }
}
