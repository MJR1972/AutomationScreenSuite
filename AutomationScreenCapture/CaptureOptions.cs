namespace AutomationScreenCapture
{
    public sealed class CaptureOptions
    {
        public bool IncludeCursor { get; set; }

        public byte DimOpacity { get; set; } = 140;

        public bool CopyToClipboard { get; set; }
    }
}
