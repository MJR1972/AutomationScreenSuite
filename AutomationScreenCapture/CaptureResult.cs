using System.Windows;
using System.Windows.Media.Imaging;

namespace AutomationScreenCapture
{
    public sealed class CaptureResult
    {
        public bool IsCanceled { get; set; }

        public Int32Rect SelectedRegionVirtualPixels { get; set; }

        public BitmapSource? Image { get; set; }
    }
}
