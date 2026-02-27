using System.Windows.Media.Imaging;

namespace AutomationScreenStudio.Infrastructure
{
    public interface IEditorSurface
    {
        void ClearMarkups();

        void DeleteSelected();

        BitmapSource? RenderComposite();
    }
}
