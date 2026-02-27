using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutomationScreenCapture;
using AutomationScreenStudio.Enums;
using AutomationScreenStudio.Infrastructure;
using AutomationScreenStudio.Markup;

namespace AutomationScreenStudio.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly ScreenCaptureService _screenCaptureService;
        private readonly IFileSaveService _fileSaveService;
        private BitmapSource? _capturedImage;
        private ColorOption _selectedColorOption;
        private double _selectedThickness;
        private ToolMode _selectedTool;
        private string _statusText;
        private IEditorSurface? _editorSurface;

        public MainViewModel(ScreenCaptureService screenCaptureService, IFileSaveService fileSaveService)
        {
            _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
            _fileSaveService = fileSaveService ?? throw new ArgumentNullException(nameof(fileSaveService));

            AvailableColors = new ObservableCollection<ColorOption>
            {
                new ColorOption("Red", Colors.Red),
                new ColorOption("Orange", Colors.Orange),
                new ColorOption("Yellow", Colors.Goldenrod),
                new ColorOption("Green", Colors.LimeGreen),
                new ColorOption("Blue", Colors.DodgerBlue),
                new ColorOption("Purple", Colors.MediumSlateBlue),
                new ColorOption("Black", Colors.Black),
                new ColorOption("White", Colors.White)
            };

            ToolModes = new ObservableCollection<ToolMode>(Enum.GetValues(typeof(ToolMode)).Cast<ToolMode>());

            _selectedColorOption = AvailableColors[0];
            _selectedThickness = 3;
            _selectedTool = ToolMode.Select;
            _statusText = "Ready";

            CaptureRegionCommand = new RelayCommand(async () => await CaptureRegionAsync());
            SaveImageCommand = new RelayCommand(SaveImage, () => CapturedImage != null && EditorSurface != null);
            ClearMarkupsCommand = new RelayCommand(ClearMarkups, () => EditorSurface != null);
            DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => EditorSurface != null);
        }

        public ObservableCollection<ColorOption> AvailableColors { get; }

        public ObservableCollection<ToolMode> ToolModes { get; }

        public BitmapSource? CapturedImage
        {
            get => _capturedImage;
            set
            {
                if (SetProperty(ref _capturedImage, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public Color SelectedColor
        {
            get => SelectedColorOption.Color;
            set
            {
                var match = AvailableColors.FirstOrDefault(option => option.Color == value);
                if (match != null)
                {
                    SelectedColorOption = match;
                }
            }
        }

        public ColorOption SelectedColorOption
        {
            get => _selectedColorOption;
            set
            {
                if (SetProperty(ref _selectedColorOption, value))
                {
                    OnPropertyChanged(nameof(SelectedColor));
                }
            }
        }

        public double SelectedThickness
        {
            get => _selectedThickness;
            set => SetProperty(ref _selectedThickness, value);
        }

        public ToolMode SelectedTool
        {
            get => _selectedTool;
            set => SetProperty(ref _selectedTool, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public IEditorSurface? EditorSurface
        {
            get => _editorSurface;
            set
            {
                if (SetProperty(ref _editorSurface, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public RelayCommand CaptureRegionCommand { get; }

        public RelayCommand SaveImageCommand { get; }

        public RelayCommand ClearMarkupsCommand { get; }

        public RelayCommand DeleteSelectedCommand { get; }

        private async Task CaptureRegionAsync()
        {
            StatusText = "Drag to select a screen region.";

            try
            {
                var result = await _screenCaptureService.CaptureRegionAsync(new CaptureOptions
                {
                    DimOpacity = 140,
                    IncludeCursor = false,
                    CopyToClipboard = false
                });

                if (result.IsCanceled || result.Image == null)
                {
                    StatusText = "Capture canceled.";
                    return;
                }

                CapturedImage = result.Image;
                StatusText = $"Captured {result.SelectedRegionVirtualPixels.Width} x {result.SelectedRegionVirtualPixels.Height} pixels.";
            }
            catch (Exception exception)
            {
                StatusText = $"Capture failed: {exception.Message}";
            }
        }

        private void SaveImage()
        {
            if (EditorSurface == null)
            {
                StatusText = "Editor is not available.";
                return;
            }

            var composite = EditorSurface.RenderComposite();
            if (composite == null)
            {
                StatusText = "Nothing to save.";
                return;
            }

            try
            {
                var filePath = _fileSaveService.SaveBitmap(composite);
                StatusText = string.IsNullOrEmpty(filePath)
                    ? "Save canceled."
                    : $"Saved image to {filePath}.";
            }
            catch (Exception exception)
            {
                StatusText = $"Save failed: {exception.Message}";
            }
        }

        private void ClearMarkups()
        {
            EditorSurface?.ClearMarkups();
            StatusText = "Markups cleared.";
        }

        private void DeleteSelected()
        {
            EditorSurface?.DeleteSelected();
            StatusText = "Selected markup deleted.";
        }

        private void RaiseCommandStates()
        {
            CaptureRegionCommand.RaiseCanExecuteChanged();
            SaveImageCommand.RaiseCanExecuteChanged();
            ClearMarkupsCommand.RaiseCanExecuteChanged();
            DeleteSelectedCommand.RaiseCanExecuteChanged();
        }
    }
}
