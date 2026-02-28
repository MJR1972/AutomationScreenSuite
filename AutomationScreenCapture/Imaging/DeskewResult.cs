using System.Windows.Media.Imaging;

namespace AutomationScreenCapture.Imaging
{
    public sealed class DeskewResult
    {
        public BitmapSource Image { get; set; } = null!;

        public double EstimatedSkewDegrees { get; set; }

        public bool Applied { get; set; }

        public double Confidence { get; set; }
    }
}
