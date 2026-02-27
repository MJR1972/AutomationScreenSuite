using System.Windows.Media.Imaging;

namespace AutomationScreenStudio.Infrastructure
{
    public interface IFileSaveService
    {
        string? SaveBitmap(BitmapSource image);
    }
}
