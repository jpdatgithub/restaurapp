namespace Restaurapp.BlazorServer.Services
{
    public sealed class UploadsOptions
    {
        public const string SectionName = "Uploads";

        public string RootPath { get; set; } = "uploads";

        public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    }
}