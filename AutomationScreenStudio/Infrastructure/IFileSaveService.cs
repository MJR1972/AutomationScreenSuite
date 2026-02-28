using System.Windows.Media.Imaging;
using AutomationScreenCapture.Imaging;

namespace AutomationScreenStudio.Infrastructure
{
    public interface IFileSaveService
    {
        FileSaveResult? SaveBitmap(BitmapSource image, ImageExportOptions options);
    }
}
