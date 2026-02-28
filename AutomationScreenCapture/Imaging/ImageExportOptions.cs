namespace AutomationScreenCapture.Imaging
{
    public sealed class ImageExportOptions
    {
        public ImageExportFormat Format { get; set; } = ImageExportFormat.Png;

        public int JpegQuality { get; set; } = 85;

        public bool RemoveAlphaIfOpaque { get; set; } = true;

        public bool ConvertToBgr24WhenPossible { get; set; } = true;
    }
}
