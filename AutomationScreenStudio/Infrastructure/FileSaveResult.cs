namespace AutomationScreenStudio.Infrastructure
{
    public sealed class FileSaveResult
    {
        public string FilePath { get; set; } = string.Empty;

        public long BytesWritten { get; set; }
    }
}
