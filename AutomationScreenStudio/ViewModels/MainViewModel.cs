using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutomationScreenCapture;
using AutomationScreenCapture.Imaging;
using AutomationScreenStudio.Enums;
using AutomationScreenStudio.Infrastructure;
using AutomationScreenStudio.Markup;

namespace AutomationScreenStudio.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly IFileSaveService _fileSaveService;
        private BitmapSource? _originalImage;
        private BitmapSource? _workingImage;
        private ColorOption _selectedColorOption;
        private double _selectedThickness;
        private ToolMode _selectedTool;
        private double _zoom;
        private ZoomPreset? _selectedZoomPreset;
        private string _statusText;
        private string _imageActionStatusText;
        private IEditorSurface? _editorSurface;
        private bool _isUpdatingZoomPreset;
        private bool _isBusy;
        private string _selectedDenoiseOption;
        private CaptionBubbleStyle _selectedBubbleStyle;
        private TailSide _selectedTailSide;
        private ImageExportFormat _selectedExportFormat;
        private int _jpegQuality;
        private bool _removeAlphaIfOpaque;
        private long _currentEncodedBytes;
        private int _imageInfoRequestVersion;

        public MainViewModel(IFileSaveService fileSaveService)
        {
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
            ZoomPresets = new ObservableCollection<ZoomPreset>
            {
                new ZoomPreset("25%", 0.25),
                new ZoomPreset("50%", 0.50),
                new ZoomPreset("75%", 0.75),
                new ZoomPreset("100%", 1.00),
                new ZoomPreset("125%", 1.25),
                new ZoomPreset("150%", 1.50),
                new ZoomPreset("200%", 2.00),
                new ZoomPreset("400%", 4.00)
            };
            BubbleStyles = new ObservableCollection<CaptionBubbleStyle>(Enum.GetValues(typeof(CaptionBubbleStyle)).Cast<CaptionBubbleStyle>());
            TailSides = new ObservableCollection<TailSide>(Enum.GetValues(typeof(TailSide)).Cast<TailSide>());
            DenoiseOptions = new ObservableCollection<string>
            {
                "Off",
                "Low",
                "Medium",
                "High"
            };
            ExportFormats = new ObservableCollection<ImageExportFormat>(Enum.GetValues(typeof(ImageExportFormat)).Cast<ImageExportFormat>());
            JpegQualityOptions = new ObservableCollection<int> { 60, 75, 85, 95 };

            _selectedColorOption = AvailableColors[0];
            _selectedThickness = 3;
            _selectedTool = ToolMode.Select;
            _zoom = 1.0;
            _statusText = "Ready";
            _imageActionStatusText = "No image action applied.";
            _selectedDenoiseOption = DenoiseOptions[0];
            _selectedBubbleStyle = CaptionBubbleStyle.SpeechRounded;
            _selectedTailSide = TailSide.Right;
            _selectedExportFormat = ImageExportFormat.Png;
            _jpegQuality = 85;
            _removeAlphaIfOpaque = true;

            UpdateSelectedZoomPreset();

            CaptureRegionCommand = new RelayCommand(async () => await CaptureRegionAsync(), () => !IsBusy);
            SaveImageCommand = new RelayCommand(SaveImage, () => WorkingImage != null && EditorSurface != null && !IsBusy);
            ClearMarkupsCommand = new RelayCommand(ClearMarkups, () => EditorSurface != null && !IsBusy);
            DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => EditorSurface != null && !IsBusy);
            ZoomInCommand = new RelayCommand(ZoomIn);
            ZoomOutCommand = new RelayCommand(ZoomOut);
            ZoomResetCommand = new RelayCommand(ZoomReset);
            ZoomFitCommand = new RelayCommand(ZoomFit);
            ApplyGrayscaleCommand = new RelayCommand(async () => await ApplyImageTransformAsync(
                "Grayscale applied.",
                image => ImageProcessingService.ToGrayscale(image)), CanTransformImage);
            ApplyInvertCommand = new RelayCommand(async () => await ApplyImageTransformAsync(
                "Invert applied.",
                image => ImageProcessingService.Invert(image)), CanTransformImage);
            ApplyDeskewCommand = new RelayCommand(async () => await ApplyDeskewAsync(), CanTransformImage);
            RotateLeft90Command = new RelayCommand(async () => await ApplyImageTransformAsync(
                "Rotated left 90 degrees.",
                image => ImageProcessingService.Rotate90CCW(image)), CanTransformImage);
            RotateRight90Command = new RelayCommand(async () => await ApplyImageTransformAsync(
                "Rotated right 90 degrees.",
                image => ImageProcessingService.Rotate90CW(image)), CanTransformImage);
            RotateLeft45Command = new RelayCommand(async () => await ApplyImageTransformAsync(
                "Rotated left 45 degrees.",
                image => ImageProcessingService.Rotate45CCW(image)), CanTransformImage);
            RotateRight45Command = new RelayCommand(async () => await ApplyImageTransformAsync(
                "Rotated right 45 degrees.",
                image => ImageProcessingService.Rotate45CW(image)), CanTransformImage);
            ApplyDenoiseCommand = new RelayCommand(async () => await ApplyDenoiseAsync(), CanTransformImage);
            SetDenoiseOptionCommand = new RelayCommand(async parameter => await SetDenoiseOptionAsync(parameter), _ => !IsBusy);
            ResetImageCommand = new RelayCommand(ResetImage, () => OriginalImage != null && !IsBusy);
        }

        public ObservableCollection<ColorOption> AvailableColors { get; }

        public ObservableCollection<ToolMode> ToolModes { get; }

        public ObservableCollection<ZoomPreset> ZoomPresets { get; }

        public ObservableCollection<string> DenoiseOptions { get; }

        public ObservableCollection<CaptionBubbleStyle> BubbleStyles { get; }

        public ObservableCollection<TailSide> TailSides { get; }

        public ObservableCollection<ImageExportFormat> ExportFormats { get; }

        public ObservableCollection<int> JpegQualityOptions { get; }

        public BitmapSource? OriginalImage
        {
            get => _originalImage;
            set => SetProperty(ref _originalImage, value);
        }

        public BitmapSource? WorkingImage
        {
            get => _workingImage;
            set
            {
                if (SetProperty(ref _workingImage, value))
                {
                    RaiseCommandStates();
                    TriggerImageInfoRefresh();
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

        public CaptionBubbleStyle SelectedBubbleStyle
        {
            get => _selectedBubbleStyle;
            set => SetProperty(ref _selectedBubbleStyle, value);
        }

        public TailSide SelectedTailSide
        {
            get => _selectedTailSide;
            set => SetProperty(ref _selectedTailSide, value);
        }

        public double Zoom
        {
            get => _zoom;
            set
            {
                var clamped = ClampZoom(value);
                if (SetProperty(ref _zoom, clamped))
                {
                    OnPropertyChanged(nameof(ZoomPercentText));
                    UpdateSelectedZoomPreset();
                }
            }
        }

        public string ZoomPercentText => $"{(int)Math.Round(Zoom * 100)}%";

        public ZoomPreset? SelectedZoomPreset
        {
            get => _selectedZoomPreset;
            set
            {
                if (SetProperty(ref _selectedZoomPreset, value) && !_isUpdatingZoomPreset && value != null)
                {
                    Zoom = value.Value;
                }
            }
        }

        public string SelectedDenoiseOption
        {
            get => _selectedDenoiseOption;
            set => SetProperty(ref _selectedDenoiseOption, value);
        }

        public ImageExportFormat SelectedExportFormat
        {
            get => _selectedExportFormat;
            set
            {
                if (SetProperty(ref _selectedExportFormat, value))
                {
                    OnPropertyChanged(nameof(ImageInfoText));
                    TriggerImageInfoRefresh();
                }
            }
        }

        public int JpegQuality
        {
            get => _jpegQuality;
            set
            {
                var clamped = Math.Max(1, Math.Min(100, value));
                if (SetProperty(ref _jpegQuality, clamped))
                {
                    OnPropertyChanged(nameof(ImageInfoText));
                    TriggerImageInfoRefresh();
                }
            }
        }

        public bool RemoveAlphaIfOpaque
        {
            get => _removeAlphaIfOpaque;
            set
            {
                if (SetProperty(ref _removeAlphaIfOpaque, value))
                {
                    TriggerImageInfoRefresh();
                }
            }
        }

        public long CurrentEncodedBytes
        {
            get => _currentEncodedBytes;
            private set
            {
                if (SetProperty(ref _currentEncodedBytes, value))
                {
                    OnPropertyChanged(nameof(ImageInfoText));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ImageActionStatusText
        {
            get => _imageActionStatusText;
            set
            {
                if (SetProperty(ref _imageActionStatusText, value))
                {
                    OnPropertyChanged(nameof(ImageInfoText));
                }
            }
        }

        public string ImageInfoText
        {
            get
            {
                if (WorkingImage == null)
                {
                    return ImageActionStatusText;
                }

                var formatText = SelectedExportFormat == ImageExportFormat.Jpeg
                    ? $"JPEG (Q={JpegQuality})"
                    : "PNG";

                return $"Captured {WorkingImage.PixelWidth}x{WorkingImage.PixelHeight}. {formatText} {FormatBytes(CurrentEncodedBytes)}. {ImageActionStatusText}";
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStates();
                }
            }
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

        public Func<Task<CaptureResult>>? CaptureRegionAsyncHandler { get; set; }

        public Func<double>? FitZoomProvider { get; set; }

        public RelayCommand CaptureRegionCommand { get; }

        public RelayCommand SaveImageCommand { get; }

        public RelayCommand ClearMarkupsCommand { get; }

        public RelayCommand DeleteSelectedCommand { get; }

        public RelayCommand ZoomInCommand { get; }

        public RelayCommand ZoomOutCommand { get; }

        public RelayCommand ZoomResetCommand { get; }

        public RelayCommand ZoomFitCommand { get; }

        public RelayCommand ApplyGrayscaleCommand { get; }

        public RelayCommand ApplyInvertCommand { get; }

        public RelayCommand ApplyDeskewCommand { get; }

        public RelayCommand RotateLeft90Command { get; }

        public RelayCommand RotateRight90Command { get; }

        public RelayCommand RotateLeft45Command { get; }

        public RelayCommand RotateRight45Command { get; }

        public RelayCommand ApplyDenoiseCommand { get; }

        public RelayCommand SetDenoiseOptionCommand { get; }

        public RelayCommand ResetImageCommand { get; }

        private async Task CaptureRegionAsync()
        {
            StatusText = "Drag to select a screen region.";

            try
            {
                if (CaptureRegionAsyncHandler == null)
                {
                    StatusText = "Capture is not available.";
                    return;
                }

                IsBusy = true;
                var result = await CaptureRegionAsyncHandler();

                if (result.IsCanceled || result.Image == null)
                {
                    StatusText = "Capture canceled.";
                    return;
                }

                OriginalImage = result.Image;
                WorkingImage = result.Image;
                ImageActionStatusText = "Original image loaded.";
                StatusText = $"Captured {result.SelectedRegionVirtualPixels.Width} x {result.SelectedRegionVirtualPixels.Height} pixels.";
            }
            catch (Exception exception)
            {
                StatusText = $"Capture failed: {exception.Message}";
            }
            finally
            {
                IsBusy = false;
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
                var result = _fileSaveService.SaveBitmap(composite, CreateExportOptions());
                StatusText = result == null
                    ? "Save canceled."
                    : $"Saved: {result.FilePath} ({FormatBytes(result.BytesWritten)}).";
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

        private void ZoomIn()
        {
            Zoom += 0.10;
        }

        private void ZoomOut()
        {
            Zoom -= 0.10;
        }

        private void ZoomReset()
        {
            Zoom = 1.0;
        }

        private void ZoomFit()
        {
            if (FitZoomProvider == null)
            {
                return;
            }

            Zoom = FitZoomProvider();
        }

        private async Task ApplyImageTransformAsync(string successMessage, Func<BitmapSource, BitmapSource> transform)
        {
            if (WorkingImage == null)
            {
                return;
            }

            try
            {
                IsBusy = true;
                var source = PrepareForBackgroundProcessing(WorkingImage);
                var result = await Task.Run(() => transform(source));

                ClearMarkupsForImageTransform();
                WorkingImage = result;
                StatusText = "Image updated.";
                ImageActionStatusText = successMessage;
            }
            catch (Exception exception)
            {
                StatusText = $"Image action failed: {exception.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApplyDeskewAsync()
        {
            if (WorkingImage == null)
            {
                return;
            }

            try
            {
                IsBusy = true;
                var source = PrepareForBackgroundProcessing(WorkingImage);
                var result = await Task.Run(() => ImageProcessingService.Deskew(source));

                if (!result.Applied)
                {
                    StatusText = "Deskew analysis completed.";
                    ImageActionStatusText = $"Deskew skipped. Confidence {result.Confidence:F2}.";
                    return;
                }

                ClearMarkupsForImageTransform();
                WorkingImage = result.Image;
                StatusText = "Deskew applied.";
                ImageActionStatusText = $"Deskew applied: {result.EstimatedSkewDegrees:F1} deg (confidence {result.Confidence:F2}).";
            }
            catch (Exception exception)
            {
                StatusText = $"Deskew failed: {exception.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApplyDenoiseAsync()
        {
            if (WorkingImage == null)
            {
                return;
            }

            if (string.Equals(SelectedDenoiseOption, "Off", StringComparison.OrdinalIgnoreCase))
            {
                ImageActionStatusText = "Denoise skipped: Off selected.";
                return;
            }

            var strength = (DenoiseStrength)Enum.Parse(typeof(DenoiseStrength), SelectedDenoiseOption);
            await ApplyImageTransformAsync($"Denoise applied: {SelectedDenoiseOption}.", image => ImageProcessingService.Denoise(image, strength));
        }

        private async Task SetDenoiseOptionAsync(object? parameter)
        {
            var option = parameter as string;
            if (string.IsNullOrWhiteSpace(option))
            {
                return;
            }

            SelectedDenoiseOption = option!;
            if (string.Equals(option, "Off", StringComparison.OrdinalIgnoreCase))
            {
                ImageActionStatusText = "Denoise mode set to Off.";
                return;
            }

            await ApplyDenoiseAsync();
        }

        private void ResetImage()
        {
            if (OriginalImage == null)
            {
                return;
            }

            ClearMarkupsForImageTransform();
            WorkingImage = OriginalImage;
            StatusText = "Image reset.";
            ImageActionStatusText = "Image reset to original.";
        }

        private void ClearMarkupsForImageTransform()
        {
            EditorSurface?.ClearMarkups();
            StatusText = "Markups cleared due to image transform.";
        }

        private void TriggerImageInfoRefresh()
        {
            var requestVersion = ++_imageInfoRequestVersion;
            var image = WorkingImage;
            if (image == null)
            {
                CurrentEncodedBytes = 0;
                OnPropertyChanged(nameof(ImageInfoText));
                return;
            }

            var options = CreateExportOptions();
            _ = RefreshImageInfoAsync(image, options, requestVersion);
        }

        private async Task RefreshImageInfoAsync(BitmapSource image, ImageExportOptions options, int requestVersion)
        {
            try
            {
                var source = PrepareForBackgroundProcessing(image);
                var size = await Task.Run(() => ImageExportService.GetEncodedSizeBytes(source, options));
                if (requestVersion != _imageInfoRequestVersion)
                {
                    return;
                }

                CurrentEncodedBytes = size;
            }
            catch
            {
                if (requestVersion == _imageInfoRequestVersion)
                {
                    CurrentEncodedBytes = 0;
                }
            }
        }

        private ImageExportOptions CreateExportOptions()
        {
            return new ImageExportOptions
            {
                Format = SelectedExportFormat,
                JpegQuality = JpegQuality,
                RemoveAlphaIfOpaque = RemoveAlphaIfOpaque,
                ConvertToBgr24WhenPossible = true
            };
        }

        private static BitmapSource PrepareForBackgroundProcessing(BitmapSource source)
        {
            if (!source.IsFrozen && source.CanFreeze)
            {
                source.Freeze();
            }

            return source;
        }

        private bool CanTransformImage()
        {
            return WorkingImage != null && !IsBusy;
        }

        private void UpdateSelectedZoomPreset()
        {
            _isUpdatingZoomPreset = true;
            try
            {
                SelectedZoomPreset = ZoomPresets.FirstOrDefault(preset => Math.Abs(preset.Value - Zoom) < 0.001);
            }
            finally
            {
                _isUpdatingZoomPreset = false;
            }
        }

        private static double ClampZoom(double zoom)
        {
            return Math.Max(0.25, Math.Min(4.0, zoom));
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L)
            {
                return $"{bytes / (1024d * 1024d):F2} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024d:F0} KB";
            }

            return $"{bytes} B";
        }

        private void RaiseCommandStates()
        {
            CaptureRegionCommand.RaiseCanExecuteChanged();
            SaveImageCommand.RaiseCanExecuteChanged();
            ClearMarkupsCommand.RaiseCanExecuteChanged();
            DeleteSelectedCommand.RaiseCanExecuteChanged();
            ApplyGrayscaleCommand.RaiseCanExecuteChanged();
            ApplyInvertCommand.RaiseCanExecuteChanged();
            ApplyDeskewCommand.RaiseCanExecuteChanged();
            RotateLeft90Command.RaiseCanExecuteChanged();
            RotateRight90Command.RaiseCanExecuteChanged();
            RotateLeft45Command.RaiseCanExecuteChanged();
            RotateRight45Command.RaiseCanExecuteChanged();
            ApplyDenoiseCommand.RaiseCanExecuteChanged();
            SetDenoiseOptionCommand.RaiseCanExecuteChanged();
            ResetImageCommand.RaiseCanExecuteChanged();
        }
    }
}
