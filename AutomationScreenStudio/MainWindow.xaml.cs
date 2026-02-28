using System.Threading.Tasks;
using System.Windows;
using AutomationScreenCapture;
using AutomationScreenStudio.Infrastructure;
using AutomationScreenStudio.ViewModels;

namespace AutomationScreenStudio
{
    public partial class MainWindow : Window
    {
        private readonly ScreenCaptureService _screenCaptureService = new ScreenCaptureService();

        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new MainViewModel(new FileSaveService())
            {
                EditorSurface = Editor,
                CaptureRegionAsyncHandler = CaptureWithWindowHiddenAsync,
                FitZoomProvider = () => Editor.CalculateFitZoom()
            };

            DataContext = viewModel;
        }

        private async Task<CaptureResult> CaptureWithWindowHiddenAsync()
        {
            var previousWindowState = WindowState;

            try
            {
                Hide();
                await Task.Delay(150);

                return await _screenCaptureService.CaptureRegionAsync(new CaptureOptions
                {
                    DimOpacity = 140,
                    IncludeCursor = false,
                    CopyToClipboard = false
                });
            }
            finally
            {
                Show();
                WindowState = previousWindowState;
                Activate();
                Topmost = true;
                Topmost = false;
                Activate();
            }
        }

        private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
