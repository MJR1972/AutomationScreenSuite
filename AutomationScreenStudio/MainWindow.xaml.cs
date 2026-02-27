using System.Windows;
using AutomationScreenCapture;
using AutomationScreenStudio.Infrastructure;
using AutomationScreenStudio.ViewModels;

namespace AutomationScreenStudio
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new MainViewModel(new ScreenCaptureService(), new FileSaveService())
            {
                EditorSurface = Editor
            };

            DataContext = viewModel;
        }
    }
}
