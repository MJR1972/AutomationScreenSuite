using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using AutomationScreenStudio.Enums;
using AutomationScreenStudio.Infrastructure;
using AutomationScreenStudio.Markup;

namespace AutomationScreenStudio.Controls
{
    public partial class EditorControl : UserControl, IEditorSurface
    {
        public static readonly DependencyProperty CapturedImageProperty =
            DependencyProperty.Register(
                nameof(CapturedImage),
                typeof(BitmapSource),
                typeof(EditorControl),
                new PropertyMetadata(null, OnCapturedImageChanged));

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(EditorControl),
                new PropertyMetadata(Colors.Red, OnSelectedStyleChanged));

        public static readonly DependencyProperty SelectedThicknessProperty =
            DependencyProperty.Register(
                nameof(SelectedThickness),
                typeof(double),
                typeof(EditorControl),
                new PropertyMetadata(3d, OnSelectedStyleChanged));

        public static readonly DependencyProperty SelectedToolProperty =
            DependencyProperty.Register(
                nameof(SelectedTool),
                typeof(ToolMode),
                typeof(EditorControl),
                new PropertyMetadata(ToolMode.Select));

        public static readonly DependencyProperty SelectedBubbleStyleProperty =
            DependencyProperty.Register(
                nameof(SelectedBubbleStyle),
                typeof(CaptionBubbleStyle),
                typeof(EditorControl),
                new PropertyMetadata(CaptionBubbleStyle.SpeechRounded, OnSelectedBubbleStyleChanged));

        public static readonly DependencyProperty SelectedTailSideProperty =
            DependencyProperty.Register(
                nameof(SelectedTailSide),
                typeof(TailSide),
                typeof(EditorControl),
                new PropertyMetadata(TailSide.Right, OnSelectedTailSideChanged));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(
                nameof(Zoom),
                typeof(double),
                typeof(EditorControl),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnZoomChanged));

        private const double HandleSize = 10;
        private const double MinimumElementSize = 5;

        private readonly List<MarkupBase> _markups = new List<MarkupBase>();
        private readonly Dictionary<UIElement, MarkupBase> _elementToMarkup = new Dictionary<UIElement, MarkupBase>();
        private readonly Dictionary<UIElement, SelectionState> _selectionStates = new Dictionary<UIElement, SelectionState>();
        private MarkupBase? _activeMarkup;
        private UIElement? _selectedElement;
        private Point _dragStart;
        private Point _lastMovePoint;
        private bool _isDrawing;
        private TextBox? _textEditor;
        private bool _isEditingBubbleText;
        private FrameworkElement? _editingBubbleRoot;
        private TextBox? _activeBubbleTextBox;
        private string _originalBubbleText = string.Empty;
        private bool _originalBubblePlaceholderVisible;
        private int _zIndexSeed;
        private DragMode _dragMode;
        private string? _activeHandleId;
        private Point _dragStartMousePoint;
        private Rect _dragStartBounds;
        private Point _dragStartPrimaryPoint;
        private Point _dragStartSecondaryPoint;

        public EditorControl()
        {
            InitializeComponent();
        }

        public BitmapSource? CapturedImage
        {
            get => (BitmapSource?)GetValue(CapturedImageProperty);
            set => SetValue(CapturedImageProperty, value);
        }

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public double SelectedThickness
        {
            get => (double)GetValue(SelectedThicknessProperty);
            set => SetValue(SelectedThicknessProperty, value);
        }

        public ToolMode SelectedTool
        {
            get => (ToolMode)GetValue(SelectedToolProperty);
            set => SetValue(SelectedToolProperty, value);
        }

        public CaptionBubbleStyle SelectedBubbleStyle
        {
            get => (CaptionBubbleStyle)GetValue(SelectedBubbleStyleProperty);
            set => SetValue(SelectedBubbleStyleProperty, value);
        }

        public TailSide SelectedTailSide
        {
            get => (TailSide)GetValue(SelectedTailSideProperty);
            set => SetValue(SelectedTailSideProperty, value);
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public void ClearMarkups()
        {
            CommitBubbleEdit();
            CommitPendingText();
            ClearSelection();
            OverlayCanvas.Children.Clear();
            HandlesCanvas.Children.Clear();
            _markups.Clear();
            _elementToMarkup.Clear();
            _selectionStates.Clear();
            _activeMarkup = null;
            ResetDragState();
        }

        public void DeleteSelected()
        {
            CommitBubbleEdit();
            CommitPendingText();
            if (_selectedElement == null)
            {
                return;
            }

            RemoveMarkupElement(_selectedElement);
        }

        public BitmapSource? RenderComposite()
        {
            CommitBubbleEdit();
            CommitPendingText();
            if (CapturedImage == null)
            {
                return null;
            }

            var width = CapturedImage.PixelWidth;
            var height = CapturedImage.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawImage(CapturedImage, new Rect(0, 0, width, height));
                foreach (var markup in _markups)
                {
                    markup.Draw(drawingContext);
                }
            }

            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            renderTarget.Freeze();
            return renderTarget;
        }

        private static void OnCapturedImageChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            ((EditorControl)dependencyObject).OnCapturedImageChanged((BitmapSource?)e.NewValue);
        }

        private static void OnSelectedStyleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            ((EditorControl)dependencyObject).OnSelectedStyleChanged();
        }

        private static void OnZoomChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            ((EditorControl)dependencyObject).OnZoomChanged((double)e.NewValue);
        }

        private static void OnSelectedBubbleStyleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            ((EditorControl)dependencyObject).OnSelectedBubbleStyleChanged((CaptionBubbleStyle)e.NewValue);
        }

        private static void OnSelectedTailSideChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            ((EditorControl)dependencyObject).OnSelectedTailSideChanged((TailSide)e.NewValue);
        }

        private void OnCapturedImageChanged(BitmapSource? image)
        {
            BaseImage.Source = image;
            if (image == null)
            {
                SurfaceGrid.Width = 0;
                SurfaceGrid.Height = 0;
            }
            else
            {
                SurfaceGrid.Width = image.PixelWidth;
                SurfaceGrid.Height = image.PixelHeight;
            }

            ClearMarkups();
        }

        private void OnSelectedStyleChanged()
        {
            if (_selectedElement == null)
            {
                return;
            }

            ApplyStyleToElement(_selectedElement, SelectedColor, SelectedThickness);
        }

        private void OnSelectedBubbleStyleChanged(CaptionBubbleStyle style)
        {
            if (_selectedElement == null)
            {
                return;
            }

            if (!TryGetCaptionBubble(_selectedElement, out var bubbleRoot, out var data))
            {
                return;
            }

            data.Style = style;
            ApplyCaptionBubbleVisuals(bubbleRoot, data);
        }

        private void OnSelectedTailSideChanged(TailSide tailSide)
        {
            if (_selectedElement == null)
            {
                return;
            }

            if (!TryGetCaptionBubble(_selectedElement, out var bubbleRoot, out var data))
            {
                return;
            }

            data.TailSide = tailSide;
            ApplyCaptionBubbleVisuals(bubbleRoot, data);
        }

        private void OnZoomChanged(double zoom)
        {
            var clampedZoom = ClampZoom(zoom);
            if (Math.Abs(clampedZoom - zoom) > 0.0001)
            {
                Zoom = clampedZoom;
                return;
            }

            if (SurfaceScaleTransform != null)
            {
                SurfaceScaleTransform.ScaleX = clampedZoom;
                SurfaceScaleTransform.ScaleY = clampedZoom;
            }
        }

        public double CalculateFitZoom(bool clampTo100 = true)
        {
            if (CapturedImage == null ||
                CapturedImage.PixelWidth <= 0 ||
                CapturedImage.PixelHeight <= 0 ||
                EditorScrollViewer.ViewportWidth <= 0 ||
                EditorScrollViewer.ViewportHeight <= 0)
            {
                return 1.0;
            }

            var zoomX = EditorScrollViewer.ViewportWidth / CapturedImage.PixelWidth;
            var zoomY = EditorScrollViewer.ViewportHeight / CapturedImage.PixelHeight;
            var fitZoom = Math.Min(zoomX, zoomY);
            fitZoom = ClampZoom(fitZoom);
            if (clampTo100)
            {
                fitZoom = Math.Min(fitZoom, 1.0);
            }

            return fitZoom;
        }

        private void OverlayCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (CapturedImage == null)
            {
                return;
            }

            Focus();
            if (_isEditingBubbleText)
            {
                if (IsEditingBubbleTextSource(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                return;
            }

            if (_textEditor != null && IsEditingText(e.OriginalSource as DependencyObject))
            {
                return;
            }

            CommitPendingText();

            var point = GetCanvasPoint(e);
            _dragStart = point;
            _lastMovePoint = point;

            var hitElement = FindMarkupElement(e.OriginalSource as DependencyObject);
            if (hitElement != null)
            {
                if (e.ClickCount >= 2 && TryGetCaptionBubble(hitElement, out var bubbleRoot, out _))
                {
                    SelectElement(bubbleRoot);
                    BeginEditBubbleText(bubbleRoot);
                    e.Handled = true;
                    return;
                }

                var wasAlreadySelected = ReferenceEquals(_selectedElement, hitElement);
                SelectElement(hitElement);
                if (SelectedTool == ToolMode.Select || wasAlreadySelected)
                {
                    _dragMode = DragMode.MoveElement;
                    OverlayCanvas.CaptureMouse();
                }

                e.Handled = true;
                return;
            }

            if (_selectedElement != null)
            {
                ClearSelection();
                e.Handled = true;
                return;
            }

            if (SelectedTool == ToolMode.Text)
            {
                StartTextEntry(point);
                e.Handled = true;
                return;
            }

            if (SelectedTool == ToolMode.Select)
            {
                e.Handled = true;
                return;
            }

            _activeMarkup = CreateMarkup(SelectedTool, point, point);
            if (_activeMarkup == null)
            {
                return;
            }

            AddMarkupElement(_activeMarkup);
            SelectElement(_activeMarkup.VisualElement as UIElement);
            _isDrawing = true;
            OverlayCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void OverlayCanvas_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isEditingBubbleText)
            {
                return;
            }

            var point = GetCanvasPoint(e);

            if (_isDrawing && _activeMarkup != null)
            {
                UpdateMarkupGeometry(_activeMarkup, _dragStart, point);
                _activeMarkup.UpdateVisual(true);
                return;
            }

            if (_dragMode == DragMode.ResizeHandle)
            {
                ResizeSelectedElement(point);
                return;
            }

            if (_dragMode == DragMode.MoveElement && _selectedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var delta = point - _lastMovePoint;
                MoveSelectedElement(delta);
                _lastMovePoint = point;
            }
        }

        private void OverlayCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isEditingBubbleText)
            {
                return;
            }

            if (_isDrawing)
            {
                _isDrawing = false;
                OverlayCanvas.ReleaseMouseCapture();

                if (_activeMarkup != null && _activeMarkup.IsEmpty())
                {
                    var activeElement = _activeMarkup.VisualElement as UIElement;
                    if (activeElement != null)
                    {
                        RemoveMarkupElement(activeElement);
                    }
                }

                _activeMarkup = null;
                UpdateSelectionHandles();
            }

            if (_dragMode == DragMode.MoveElement)
            {
                OverlayCanvas.ReleaseMouseCapture();
            }
            else if (_dragMode == DragMode.ResizeHandle)
            {
                HandlesCanvas.ReleaseMouseCapture();
            }

            ResetDragState();
        }

        private void Handle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isEditingBubbleText)
            {
                return;
            }

            if (_selectedElement == null || !(sender is FrameworkElement handle) || !(handle.Tag is string handleId))
            {
                return;
            }

            _dragMode = DragMode.ResizeHandle;
            _activeHandleId = handleId;
            _dragStartMousePoint = GetCanvasPoint(e);
            StoreResizeSnapshot();
            HandlesCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void StartTextEntry(Point location)
        {
            CancelPendingText();
            ClearSelection();

            _textEditor = new TextBox
            {
                MinWidth = 50,
                FontSize = GetTextFontSize(),
                Foreground = new SolidColorBrush(SelectedColor),
                Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                AcceptsReturn = false
            };

            _textEditor.KeyDown += TextEditor_OnKeyDown;
            _textEditor.LostKeyboardFocus += TextEditor_OnLostKeyboardFocus;

            Canvas.SetLeft(_textEditor, location.X);
            Canvas.SetTop(_textEditor, location.Y);
            OverlayCanvas.Children.Add(_textEditor);
            Canvas.SetZIndex(_textEditor, ++_zIndexSeed);
            _textEditor.Focus();
            _textEditor.CaretIndex = _textEditor.Text.Length;
        }

        private void BeginEditBubbleText(FrameworkElement bubbleRoot)
        {
            if (_isEditingBubbleText || !TryGetCaptionBubble(bubbleRoot, out var resolvedRoot, out var data))
            {
                return;
            }

            var container = resolvedRoot is Border border && border.Child is Panel panel ? panel : null;
            if (container == null)
            {
                return;
            }

            SelectElement(resolvedRoot);

            var existingTextBlock = FindBubbleTextBlock(container);
            double left;
            double top;
            double width;
            double height;

            if (existingTextBlock != null)
            {
                left = Canvas.GetLeft(existingTextBlock);
                top = Canvas.GetTop(existingTextBlock);
                width = existingTextBlock.Width > 0 ? existingTextBlock.Width : System.Math.Max(40, resolvedRoot.Width - 20);
                height = existingTextBlock.ActualHeight > 0 ? existingTextBlock.ActualHeight : System.Math.Max(30, resolvedRoot.Height - 28);
                container.Children.Remove(existingTextBlock);
            }
            else if (_elementToMarkup.TryGetValue(resolvedRoot, out var markup) && markup is CaptionBubbleMarkup bubbleMarkup)
            {
                var textBounds = bubbleMarkup.GetTextBounds();
                var rootLeft = Canvas.GetLeft(resolvedRoot);
                var rootTop = Canvas.GetTop(resolvedRoot);
                left = textBounds.Left - (double.IsNaN(rootLeft) ? 0 : rootLeft);
                top = textBounds.Top - (double.IsNaN(rootTop) ? 0 : rootTop);
                width = textBounds.Width;
                height = textBounds.Height;
            }
            else
            {
                left = 10;
                top = 10;
                width = System.Math.Max(40, resolvedRoot.Width - 20);
                height = System.Math.Max(30, resolvedRoot.Height - 28);
            }

            var textBox = new TextBox
            {
                MinWidth = 40,
                Width = width,
                Height = height,
                FontSize = data.FontSize > 0 ? data.FontSize : (12 + (data.StrokeThickness * 1.5)),
                Foreground = new SolidColorBrush(data.StrokeColor),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalContentAlignment = VerticalAlignment.Top
            };

            textBox.KeyDown += BubbleTextBox_OnKeyDown;
            textBox.LostKeyboardFocus += BubbleTextBox_OnLostKeyboardFocus;

            Canvas.SetLeft(textBox, left);
            Canvas.SetTop(textBox, top);
            container.Children.Add(textBox);

            _editingBubbleRoot = resolvedRoot;
            _activeBubbleTextBox = textBox;
            _originalBubbleText = data.Text ?? string.Empty;
            _originalBubblePlaceholderVisible = data.IsPlaceholderVisible;
            _isEditingBubbleText = true;

            textBox.Text = data.IsPlaceholderVisible ? string.Empty : (data.Text ?? string.Empty);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }), DispatcherPriority.ApplicationIdle);
        }

        private void TextEditor_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitPendingText();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelPendingText();
                e.Handled = true;
            }
        }

        private void BubbleTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelBubbleEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CommitBubbleEdit();
                e.Handled = true;
            }
        }

        private void BubbleTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitBubbleEdit();
        }

        private void TextEditor_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitPendingText();
        }

        private void CommitPendingText()
        {
            if (_textEditor == null)
            {
                return;
            }

            var rawText = _textEditor.Text ?? string.Empty;
            var text = rawText.Trim();
            var location = new Point(Canvas.GetLeft(_textEditor), Canvas.GetTop(_textEditor));
            OverlayCanvas.Children.Remove(_textEditor);
            _textEditor.KeyDown -= TextEditor_OnKeyDown;
            _textEditor.LostKeyboardFocus -= TextEditor_OnLostKeyboardFocus;
            _textEditor = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var markup = new TextMarkup(SelectedColor, SelectedThickness, location, text!);
            AddMarkupElement(markup);
            SelectElement(markup.VisualElement as UIElement);
        }

        private void CancelPendingText()
        {
            if (_textEditor == null)
            {
                return;
            }

            OverlayCanvas.Children.Remove(_textEditor);
            _textEditor.KeyDown -= TextEditor_OnKeyDown;
            _textEditor.LostKeyboardFocus -= TextEditor_OnLostKeyboardFocus;
            _textEditor = null;
        }

        private void EditorControl_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            if (_isEditingBubbleText)
            {
                CancelBubbleEdit();
                e.Handled = true;
                return;
            }

            if (_textEditor != null)
            {
                CancelPendingText();
                e.Handled = true;
                return;
            }

            if (_selectedElement != null)
            {
                ClearSelection();
                e.Handled = true;
            }
        }

        private MarkupBase? CreateMarkup(ToolMode toolMode, Point start, Point end)
        {
            switch (toolMode)
            {
                case ToolMode.Line:
                    return new LineMarkup(SelectedColor, SelectedThickness, start, end);
                case ToolMode.Arrow:
                    return new ArrowMarkup(SelectedColor, SelectedThickness, start, end);
                case ToolMode.Rectangle:
                    return new RectangleMarkup(SelectedColor, SelectedThickness, start, end, false);
                case ToolMode.RoundedRectangle:
                    return new RectangleMarkup(SelectedColor, SelectedThickness, start, end, true);
                case ToolMode.Ellipse:
                    return new EllipseMarkup(SelectedColor, SelectedThickness, start, end);
                case ToolMode.CaptionBubble:
                    return new CaptionBubbleMarkup(SelectedColor, SelectedThickness, start, end, SelectedBubbleStyle, SelectedTailSide);
                default:
                    return null;
            }
        }

        private void UpdateMarkupGeometry(MarkupBase markup, Point start, Point end)
        {
            switch (markup)
            {
                case LineMarkup lineMarkup:
                    if (markup is ArrowMarkup || SelectedTool == ToolMode.Line)
                    {
                        end = GetCurrentDrawEndPoint(start, end);
                    }

                    lineMarkup.Start = start;
                    lineMarkup.End = end;
                    break;
                case RectangleMarkup rectangleMarkup:
                    rectangleMarkup.Start = start;
                    rectangleMarkup.End = end;
                    break;
                case EllipseMarkup ellipseMarkup:
                    ellipseMarkup.Start = start;
                    ellipseMarkup.End = end;
                    break;
                case CaptionBubbleMarkup captionBubbleMarkup:
                    captionBubbleMarkup.Start = start;
                    captionBubbleMarkup.End = end;
                    break;
            }
        }

        private Point GetCurrentDrawEndPoint(Point start, Point current)
        {
            return IsShiftDown() ? GetSnappedEndPoint(start, current) : current;
        }

        private Point GetSnappedEndPoint(Point start, Point current)
        {
            var dx = current.X - start.X;
            var dy = current.Y - start.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length < 0.5)
            {
                return current;
            }

            var angle = Math.Atan2(dy, dx);
            var step = Math.PI / 4;
            var snappedAngle = Math.Round(angle / step) * step;

            return new Point(
                start.X + (length * Math.Cos(snappedAngle)),
                start.Y + (length * Math.Sin(snappedAngle)));
        }

        private void AddMarkupElement(MarkupBase markup)
        {
            _markups.Add(markup);
            var element = (UIElement)markup.GetOrCreateVisual();
            _elementToMarkup[element] = markup;
            OverlayCanvas.Children.Add(element);
            Canvas.SetZIndex(element, ++_zIndexSeed);
        }

        private void RemoveMarkupElement(UIElement element)
        {
            RemoveSelectionVisual(element);
            if (_elementToMarkup.TryGetValue(element, out var markup))
            {
                _elementToMarkup.Remove(element);
                _markups.Remove(markup);
            }

            OverlayCanvas.Children.Remove(element);
            if (ReferenceEquals(_selectedElement, element))
            {
                _selectedElement = null;
                ClearSelectionHandles();
            }
        }

        private void MoveSelectedElement(Vector delta)
        {
            if (_selectedElement == null)
            {
                return;
            }

            if (_elementToMarkup.TryGetValue(_selectedElement, out var markup))
            {
                markup.Offset(delta);
                RefreshSelectedMarkupVisual(markup);
            }
        }

        private void ApplyStyleToElement(UIElement element, Color color, double thickness)
        {
            if (TryGetCaptionBubble(element, out var bubbleRoot, out var bubbleData))
            {
                bubbleData.StrokeColor = color;
                bubbleData.StrokeThickness = thickness;
                bubbleData.FontSize = 12 + (thickness * 1.5);
                ApplyCaptionBubbleVisuals(bubbleRoot, bubbleData);
                return;
            }

            if (!_elementToMarkup.TryGetValue(element, out var markup))
            {
                return;
            }

            markup.SetStyle(color, thickness);
            RefreshSelectedMarkupVisual(markup);
        }

        private void ResizeSelectedElement(Point mousePoint)
        {
            if (_selectedElement == null || _activeHandleId == null || !_elementToMarkup.TryGetValue(_selectedElement, out var markup))
            {
                return;
            }

            switch (markup)
            {
                case LineMarkup lineMarkup:
                    ResizeLineMarkup(lineMarkup, mousePoint);
                    break;
                case RectangleMarkup rectangleMarkup:
                    ResizeRectangularMarkup(rectangleMarkup, mousePoint);
                    break;
                case EllipseMarkup ellipseMarkup:
                    ResizeRectangularMarkup(ellipseMarkup, mousePoint);
                    break;
                case CaptionBubbleMarkup captionBubbleMarkup:
                    ResizeRectangularMarkup(captionBubbleMarkup, mousePoint);
                    break;
            }
        }

        private void ResizeLineMarkup(LineMarkup lineMarkup, Point mousePoint)
        {
            Point start;
            Point end;

            if (_activeHandleId == "LineStart")
            {
                end = _dragStartSecondaryPoint;
                start = IsShiftDown() ? GetSnappedEndPoint(end, mousePoint) : mousePoint;
            }
            else
            {
                start = _dragStartPrimaryPoint;
                end = IsShiftDown() ? GetSnappedEndPoint(start, mousePoint) : mousePoint;
            }

            lineMarkup.Start = start;
            lineMarkup.End = end;
            RefreshSelectedMarkupVisual(lineMarkup);
        }

        private void ResizeRectangularMarkup(MarkupBase markup, Point mousePoint)
        {
            var newBounds = GetResizedBounds(mousePoint, _dragStartBounds, _activeHandleId!);
            if (newBounds.Width < MinimumElementSize || newBounds.Height < MinimumElementSize)
            {
                return;
            }

            switch (markup)
            {
                case RectangleMarkup rectangleMarkup:
                    rectangleMarkup.Start = new Point(newBounds.Left, newBounds.Top);
                    rectangleMarkup.End = new Point(newBounds.Right, newBounds.Bottom);
                    RefreshSelectedMarkupVisual(rectangleMarkup);
                    break;
                case EllipseMarkup ellipseMarkup:
                    ellipseMarkup.Start = new Point(newBounds.Left, newBounds.Top);
                    ellipseMarkup.End = new Point(newBounds.Right, newBounds.Bottom);
                    RefreshSelectedMarkupVisual(ellipseMarkup);
                    break;
                case CaptionBubbleMarkup captionBubbleMarkup:
                    captionBubbleMarkup.Start = new Point(newBounds.Left, newBounds.Top);
                    captionBubbleMarkup.End = new Point(newBounds.Right, newBounds.Bottom);
                    RefreshSelectedMarkupVisual(captionBubbleMarkup);
                    break;
            }
        }

        private Rect GetResizedBounds(Point mousePoint, Rect startBounds, string handleId)
        {
            var left = startBounds.Left;
            var top = startBounds.Top;
            var right = startBounds.Right;
            var bottom = startBounds.Bottom;

            switch (handleId)
            {
                case "NW":
                    return CreateCornerResizeBounds(new Point(right, bottom), mousePoint, true, true);
                case "NE":
                    return CreateCornerResizeBounds(new Point(left, bottom), mousePoint, false, true);
                case "SE":
                    return CreateCornerResizeBounds(new Point(left, top), mousePoint, false, false);
                case "SW":
                    return CreateCornerResizeBounds(new Point(right, top), mousePoint, true, false);
                case "N":
                    top = Math.Min(mousePoint.Y, bottom - MinimumElementSize);
                    if (IsShiftDown())
                    {
                        var size = Math.Max(MinimumElementSize, bottom - top);
                        var centerX = startBounds.Left + (startBounds.Width / 2);
                        left = centerX - (size / 2);
                        right = centerX + (size / 2);
                    }

                    break;
                case "S":
                    bottom = Math.Max(mousePoint.Y, top + MinimumElementSize);
                    if (IsShiftDown())
                    {
                        var size = Math.Max(MinimumElementSize, bottom - top);
                        var centerX = startBounds.Left + (startBounds.Width / 2);
                        left = centerX - (size / 2);
                        right = centerX + (size / 2);
                    }

                    break;
                case "W":
                    left = Math.Min(mousePoint.X, right - MinimumElementSize);
                    if (IsShiftDown())
                    {
                        var size = Math.Max(MinimumElementSize, right - left);
                        var centerY = startBounds.Top + (startBounds.Height / 2);
                        top = centerY - (size / 2);
                        bottom = centerY + (size / 2);
                    }

                    break;
                case "E":
                    right = Math.Max(mousePoint.X, left + MinimumElementSize);
                    if (IsShiftDown())
                    {
                        var size = Math.Max(MinimumElementSize, right - left);
                        var centerY = startBounds.Top + (startBounds.Height / 2);
                        top = centerY - (size / 2);
                        bottom = centerY + (size / 2);
                    }

                    break;
            }

            return NormalizeRect(new Point(left, top), new Point(right, bottom));
        }

        private Rect CreateCornerResizeBounds(Point anchor, Point mousePoint, bool invertX, bool invertY)
        {
            var adjustedPoint = mousePoint;
            if (IsShiftDown())
            {
                var dx = mousePoint.X - anchor.X;
                var dy = mousePoint.Y - anchor.Y;
                var size = Math.Max(MinimumElementSize, Math.Max(Math.Abs(dx), Math.Abs(dy)));
                adjustedPoint = new Point(
                    anchor.X + ((invertX ? -1 : 1) * size),
                    anchor.Y + ((invertY ? -1 : 1) * size));
            }

            return NormalizeRect(anchor, adjustedPoint);
        }

        private void RefreshSelectedMarkupVisual(MarkupBase markup)
        {
            markup.UpdateVisual(true);
            if (_selectedElement != null)
            {
                RemoveSelectionVisual(_selectedElement);
                ApplySelectionVisual(_selectedElement);
            }

            UpdateSelectionHandles();
        }

        private UIElement? FindMarkupElement(DependencyObject? source)
        {
            var current = source;
            while (current != null && !ReferenceEquals(current, OverlayCanvas))
            {
                if (current is UIElement element &&
                    _elementToMarkup.ContainsKey(element) &&
                    !ReferenceEquals(element, _activeMarkup?.VisualElement))
                {
                    return element;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void SelectElement(UIElement? element)
        {
            if (ReferenceEquals(_selectedElement, element))
            {
                if (element != null)
                {
                    Canvas.SetZIndex(element, ++_zIndexSeed);
                    UpdateSelectionHandles();
                }

                return;
            }

            ClearSelection();
            if (element == null)
            {
                return;
            }

            _selectedElement = element;
            Canvas.SetZIndex(element, ++_zIndexSeed);
            if (_elementToMarkup.TryGetValue(element, out var markup))
            {
                markup.UpdateVisual(true);
            }

            if (TryGetCaptionBubble(element, out _, out var bubbleData))
            {
                SetCurrentValue(SelectedBubbleStyleProperty, bubbleData.Style);
                SetCurrentValue(SelectedTailSideProperty, bubbleData.TailSide);
            }

            ApplySelectionVisual(element);
            UpdateSelectionHandles();
        }

        private void ClearSelection()
        {
            if (_selectedElement == null)
            {
                ClearSelectionHandles();
                return;
            }

            var previouslySelected = _selectedElement;
            _selectedElement = null;
            RemoveSelectionVisual(previouslySelected);
            if (_elementToMarkup.TryGetValue(previouslySelected, out var markup))
            {
                markup.UpdateVisual(false);
            }

            ClearSelectionHandles();
        }

        private void ApplySelectionVisual(UIElement element)
        {
            if (_selectionStates.ContainsKey(element))
            {
                return;
            }

            var state = new SelectionState();
            if (element is Shape shape)
            {
                state.StrokeDashArray = shape.StrokeDashArray?.Clone();
                state.StrokeThickness = shape.StrokeThickness;
                shape.StrokeDashArray = new DoubleCollection { 2, 2 };
                shape.StrokeThickness = state.StrokeThickness.Value + 1;
            }
            else if (element is Border border)
            {
                state.Background = border.Background;
                state.BorderBrush = border.BorderBrush;
                state.BorderThickness = border.BorderThickness;
                border.BorderBrush = Brushes.Black;
                border.BorderThickness = new Thickness(2);
            }
            else if (element is TextBox textBox)
            {
                state.Background = textBox.Background;
                state.BorderBrush = textBox.BorderBrush;
                state.BorderThickness = textBox.BorderThickness;
                textBox.Background = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0));
                textBox.BorderBrush = Brushes.DimGray;
                textBox.BorderThickness = new Thickness(1);
            }

            _selectionStates[element] = state;
        }

        private void RemoveSelectionVisual(UIElement element)
        {
            if (!_selectionStates.TryGetValue(element, out var state))
            {
                return;
            }

            if (element is Shape shape)
            {
                shape.StrokeDashArray = state.StrokeDashArray;
                if (state.StrokeThickness.HasValue)
                {
                    shape.StrokeThickness = state.StrokeThickness.Value;
                }
            }
            else if (element is Border border)
            {
                border.Background = state.Background;
                border.BorderBrush = state.BorderBrush;
                border.BorderThickness = state.BorderThickness ?? new Thickness(0);
            }
            else if (element is TextBox textBox)
            {
                textBox.Background = state.Background;
                textBox.BorderBrush = state.BorderBrush;
                textBox.BorderThickness = state.BorderThickness ?? new Thickness(0);
            }

            _selectionStates.Remove(element);
        }

        private void UpdateSelectionHandles()
        {
            ClearSelectionHandles();
            if (_selectedElement == null ||
                ReferenceEquals(_selectedElement, _activeMarkup?.VisualElement) ||
                !_elementToMarkup.TryGetValue(_selectedElement, out var markup))
            {
                return;
            }

            switch (markup)
            {
                case LineMarkup lineMarkup:
                    AddHandle("LineStart", lineMarkup.Start, Cursors.Cross);
                    AddHandle("LineEnd", lineMarkup.End, Cursors.Cross);
                    break;
                case RectangleMarkup rectangleMarkup:
                    AddBoundsHandles(GetMarkupBounds(rectangleMarkup));
                    break;
                case EllipseMarkup ellipseMarkup:
                    AddBoundsHandles(GetMarkupBounds(ellipseMarkup));
                    break;
                case CaptionBubbleMarkup captionBubbleMarkup:
                    AddBoundsHandles(GetMarkupBounds(captionBubbleMarkup));
                    break;
            }
        }

        private void ClearSelectionHandles()
        {
            HandlesCanvas.Children.Clear();
        }

        private void AddBoundsHandles(Rect bounds)
        {
            AddHandle("NW", new Point(bounds.Left, bounds.Top), Cursors.SizeNWSE);
            AddHandle("N", new Point(bounds.Left + (bounds.Width / 2), bounds.Top), Cursors.SizeNS);
            AddHandle("NE", new Point(bounds.Right, bounds.Top), Cursors.SizeNESW);
            AddHandle("E", new Point(bounds.Right, bounds.Top + (bounds.Height / 2)), Cursors.SizeWE);
            AddHandle("SE", new Point(bounds.Right, bounds.Bottom), Cursors.SizeNWSE);
            AddHandle("S", new Point(bounds.Left + (bounds.Width / 2), bounds.Bottom), Cursors.SizeNS);
            AddHandle("SW", new Point(bounds.Left, bounds.Bottom), Cursors.SizeNESW);
            AddHandle("W", new Point(bounds.Left, bounds.Top + (bounds.Height / 2)), Cursors.SizeWE);
        }

        private void AddHandle(string handleId, Point center, Cursor cursor)
        {
            var handle = new Border
            {
                Width = HandleSize,
                Height = HandleSize,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Cursor = cursor,
                Tag = handleId
            };

            handle.MouseLeftButtonDown += Handle_OnMouseLeftButtonDown;
            Canvas.SetLeft(handle, center.X - (HandleSize / 2));
            Canvas.SetTop(handle, center.Y - (HandleSize / 2));
            Canvas.SetZIndex(handle, int.MaxValue);
            HandlesCanvas.Children.Add(handle);
        }

        private void StoreResizeSnapshot()
        {
            if (_selectedElement == null || !_elementToMarkup.TryGetValue(_selectedElement, out var markup))
            {
                return;
            }

            switch (markup)
            {
                case LineMarkup lineMarkup:
                    _dragStartPrimaryPoint = lineMarkup.Start;
                    _dragStartSecondaryPoint = lineMarkup.End;
                    break;
                case RectangleMarkup rectangleMarkup:
                    _dragStartBounds = GetMarkupBounds(rectangleMarkup);
                    break;
                case EllipseMarkup ellipseMarkup:
                    _dragStartBounds = GetMarkupBounds(ellipseMarkup);
                    break;
                case CaptionBubbleMarkup captionBubbleMarkup:
                    _dragStartBounds = GetMarkupBounds(captionBubbleMarkup);
                    break;
            }
        }

        private Rect GetMarkupBounds(MarkupBase markup)
        {
            switch (markup)
            {
                case RectangleMarkup rectangleMarkup when rectangleMarkup.VisualElement is FrameworkElement rectangleElement:
                    return GetElementBounds(rectangleElement);
                case EllipseMarkup ellipseMarkup when ellipseMarkup.VisualElement is FrameworkElement ellipseElement:
                    return GetElementBounds(ellipseElement);
                case TextMarkup textMarkup when textMarkup.VisualElement is FrameworkElement textElement:
                    return GetElementBounds(textElement);
                case CaptionBubbleMarkup captionBubbleMarkup when captionBubbleMarkup.VisualElement is FrameworkElement bubbleElement:
                    return GetElementBounds(bubbleElement);
                default:
                    return Rect.Empty;
            }
        }

        private Rect GetElementBounds(FrameworkElement element)
        {
            var left = Canvas.GetLeft(element);
            var top = Canvas.GetTop(element);
            if (double.IsNaN(left))
            {
                left = 0;
            }

            if (double.IsNaN(top))
            {
                top = 0;
            }

            return new Rect(left, top, element.Width, element.Height);
        }

        private bool TryGetCaptionBubble(UIElement element, out FrameworkElement bubbleRoot, out CaptionBubbleData data)
        {
            var current = element as DependencyObject;
            while (current != null)
            {
                if (current is FrameworkElement frameworkElement &&
                    frameworkElement.Tag is CaptionBubbleData bubbleData &&
                    _elementToMarkup.ContainsKey(frameworkElement))
                {
                    bubbleRoot = frameworkElement;
                    data = bubbleData;
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            bubbleRoot = null!;
            data = null!;
            return false;
        }

        private void ApplyCaptionBubbleVisuals(FrameworkElement bubbleRoot, CaptionBubbleData data)
        {
            if (!_elementToMarkup.TryGetValue(bubbleRoot, out var markup) || !(markup is CaptionBubbleMarkup bubbleMarkup))
            {
                return;
            }

            bubbleMarkup.BubbleStyle = data.Style;
            bubbleMarkup.TailSide = data.TailSide;
            bubbleMarkup.Text = data.Text ?? string.Empty;
            bubbleMarkup.IsPlaceholderVisible = data.IsPlaceholderVisible;
            bubbleMarkup.SetStyle(data.StrokeColor, data.StrokeThickness);
            bubbleMarkup.UpdateVisual(ReferenceEquals(_selectedElement, bubbleRoot));
            bubbleRoot.Tag = new CaptionBubbleData
            {
                Style = data.Style,
                TailSide = data.TailSide,
                StrokeColor = data.StrokeColor,
                StrokeThickness = data.StrokeThickness,
                FillColor = data.FillColor,
                Text = data.Text ?? string.Empty,
                FontSize = 12 + (data.StrokeThickness * 1.5),
                IsPlaceholderVisible = data.IsPlaceholderVisible
            };

            if (ReferenceEquals(_selectedElement, bubbleRoot))
            {
                RemoveSelectionVisual(bubbleRoot);
                ApplySelectionVisual(bubbleRoot);
            }

            UpdateSelectionHandles();
        }

        private void CommitBubbleEdit()
        {
            if (!_isEditingBubbleText || _editingBubbleRoot == null || _activeBubbleTextBox == null)
            {
                return;
            }

            if (!TryGetCaptionBubble(_editingBubbleRoot, out var bubbleRoot, out var data))
            {
                ClearBubbleEditState();
                return;
            }

            var editedText = (_activeBubbleTextBox.Text ?? string.Empty).Trim();
            RemoveActiveBubbleTextBox();
            data.Text = editedText;
            data.IsPlaceholderVisible = string.IsNullOrWhiteSpace(editedText);
            data.FontSize = 12 + (data.StrokeThickness * 1.5);
            ApplyCaptionBubbleVisuals(bubbleRoot, data);
            SelectElement(bubbleRoot);
            ClearBubbleEditState();
        }

        private void CancelBubbleEdit()
        {
            if (!_isEditingBubbleText || _editingBubbleRoot == null || _activeBubbleTextBox == null)
            {
                return;
            }

            if (TryGetCaptionBubble(_editingBubbleRoot, out var bubbleRoot, out var data))
            {
                RemoveActiveBubbleTextBox();
                data.Text = _originalBubbleText;
                data.IsPlaceholderVisible = _originalBubblePlaceholderVisible;
                data.FontSize = 12 + (data.StrokeThickness * 1.5);
                ApplyCaptionBubbleVisuals(bubbleRoot, data);
                SelectElement(bubbleRoot);
            }
            else
            {
                RemoveActiveBubbleTextBox();
            }

            ClearBubbleEditState();
        }

        private void RemoveActiveBubbleTextBox()
        {
            if (_editingBubbleRoot is Border border &&
                border.Child is Panel panel &&
                _activeBubbleTextBox != null)
            {
                _activeBubbleTextBox.KeyDown -= BubbleTextBox_OnKeyDown;
                _activeBubbleTextBox.LostKeyboardFocus -= BubbleTextBox_OnLostKeyboardFocus;
                panel.Children.Remove(_activeBubbleTextBox);
            }
        }

        private void ClearBubbleEditState()
        {
            _activeBubbleTextBox = null;
            _editingBubbleRoot = null;
            _originalBubbleText = string.Empty;
            _originalBubblePlaceholderVisible = false;
            _isEditingBubbleText = false;
        }

        private static TextBlock? FindBubbleTextBlock(Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is TextBlock textBlock)
                {
                    return textBlock;
                }
            }

            return null;
        }

        private bool IsShiftDown()
        {
            return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        }

        private void EditorScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }

            Zoom = e.Delta > 0 ? ClampZoom(Zoom + 0.10) : ClampZoom(Zoom - 0.10);
            e.Handled = true;
        }

        private double GetTextFontSize()
        {
            return 12 + (SelectedThickness * 1.5);
        }

        private bool IsEditingText(DependencyObject? source)
        {
            var current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, _textEditor))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private bool IsEditingBubbleTextSource(DependencyObject? source)
        {
            var current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, _activeBubbleTextBox))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private Point ClampToCanvas(Point point)
        {
            var surfaceWidth = CapturedImage != null && CapturedImage.PixelWidth > 0
                ? CapturedImage.PixelWidth
                : (SurfaceGrid.Width > 0 ? SurfaceGrid.Width : OverlayCanvas.ActualWidth);
            var surfaceHeight = CapturedImage != null && CapturedImage.PixelHeight > 0
                ? CapturedImage.PixelHeight
                : (SurfaceGrid.Height > 0 ? SurfaceGrid.Height : OverlayCanvas.ActualHeight);
            var x = Math.Max(0, Math.Min(surfaceWidth, point.X));
            var y = Math.Max(0, Math.Min(surfaceHeight, point.Y));
            return new Point(x, y);
        }

        private Point GetCanvasPoint(MouseEventArgs e)
        {
            var reference = e.Source as DependencyObject;
            while (reference != null &&
                   !ReferenceEquals(reference, OverlayCanvas) &&
                   !ReferenceEquals(reference, HandlesCanvas))
            {
                reference = VisualTreeHelper.GetParent(reference);
            }

            var sourceVisual = reference as Visual ?? OverlayCanvas;
            var sourceElement = reference as IInputElement ?? OverlayCanvas;
            var point = e.GetPosition(sourceElement);
            if (ReferenceEquals(sourceVisual, OverlayCanvas))
            {
                return ClampToCanvas(point);
            }

            var transform = sourceVisual.TransformToVisual(OverlayCanvas);
            return ClampToCanvas(transform.Transform(point));
        }

        private static Rect NormalizeRect(Point first, Point second)
        {
            return new Rect(
                Math.Min(first.X, second.X),
                Math.Min(first.Y, second.Y),
                Math.Abs(first.X - second.X),
                Math.Abs(first.Y - second.Y));
        }

        private void ResetDragState()
        {
            _dragMode = DragMode.None;
            _activeHandleId = null;
        }

        private static double ClampZoom(double zoom)
        {
            return Math.Max(0.25, Math.Min(4.0, zoom));
        }

        private enum DragMode
        {
            None,
            MoveElement,
            ResizeHandle
        }

        private sealed class SelectionState
        {
            public DoubleCollection? StrokeDashArray { get; set; }

            public double? StrokeThickness { get; set; }

            public Brush? Background { get; set; }

            public Brush? BorderBrush { get; set; }

            public Thickness? BorderThickness { get; set; }
        }
    }
}
