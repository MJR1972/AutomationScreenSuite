using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
                new PropertyMetadata(Colors.Red));

        public static readonly DependencyProperty SelectedThicknessProperty =
            DependencyProperty.Register(
                nameof(SelectedThickness),
                typeof(double),
                typeof(EditorControl),
                new PropertyMetadata(3d));

        public static readonly DependencyProperty SelectedToolProperty =
            DependencyProperty.Register(
                nameof(SelectedTool),
                typeof(ToolMode),
                typeof(EditorControl),
                new PropertyMetadata(ToolMode.Select));

        private readonly List<MarkupBase> _markups = new List<MarkupBase>();
        private readonly Dictionary<UIElement, MarkupBase> _elementToMarkup = new Dictionary<UIElement, MarkupBase>();
        private readonly Dictionary<UIElement, SelectionState> _selectionStates = new Dictionary<UIElement, SelectionState>();
        private MarkupBase? _activeMarkup;
        private UIElement? _selectedElement;
        private Point _dragStart;
        private Point _lastMovePoint;
        private bool _isDrawing;
        private bool _isMovingSelection;
        private TextBox? _textEditor;
        private int _zIndexSeed;

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

        public void ClearMarkups()
        {
            CommitPendingText();
            ClearSelection();
            OverlayCanvas.Children.Clear();
            _markups.Clear();
            _elementToMarkup.Clear();
            _selectionStates.Clear();
            _activeMarkup = null;
        }

        public void DeleteSelected()
        {
            CommitPendingText();
            if (_selectedElement == null)
            {
                return;
            }

            RemoveMarkupElement(_selectedElement);
        }

        public BitmapSource? RenderComposite()
        {
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

        private void OverlayCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (CapturedImage == null)
            {
                return;
            }

            Focus();
            if (_textEditor != null && IsEditingText(e.OriginalSource as DependencyObject))
            {
                return;
            }

            CommitPendingText();

            var point = ClampToCanvas(e.GetPosition(OverlayCanvas));
            _dragStart = point;
            _lastMovePoint = point;

            var hitElement = FindMarkupElement(e.OriginalSource as DependencyObject);
            if (hitElement != null)
            {
                SelectElement(hitElement);
                if (SelectedTool == ToolMode.Select)
                {
                    _isMovingSelection = true;
                    OverlayCanvas.CaptureMouse();
                }

                e.Handled = true;
                return;
            }

            ClearSelection();

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
            var point = ClampToCanvas(e.GetPosition(OverlayCanvas));

            if (_isDrawing && _activeMarkup != null)
            {
                UpdateMarkupGeometry(_activeMarkup, _dragStart, point);
                _activeMarkup.UpdateVisual(true);
                return;
            }

            if (_isMovingSelection && _selectedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var delta = point - _lastMovePoint;
                MoveSelectedElement(delta);
                _lastMovePoint = point;
            }
        }

        private void OverlayCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
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
            }

            if (_isMovingSelection)
            {
                _isMovingSelection = false;
                OverlayCanvas.ReleaseMouseCapture();
            }
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

        private void TextEditor_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitPendingText();
            }
            else if (e.Key == Key.Escape)
            {
                CancelPendingText();
            }
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

            var text = _textEditor.Text?.Trim();
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
                default:
                    return null;
            }
        }

        private void UpdateMarkupGeometry(MarkupBase markup, Point start, Point end)
        {
            switch (markup)
            {
                case LineMarkup lineMarkup:
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
            }
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
                markup.UpdateVisual(true);
                RemoveSelectionVisual(_selectedElement);
                ApplySelectionVisual(_selectedElement);
            }
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

            ApplySelectionVisual(element);
        }

        private void ClearSelection()
        {
            if (_selectedElement == null)
            {
                return;
            }

            var previouslySelected = _selectedElement;
            _selectedElement = null;
            RemoveSelectionVisual(previouslySelected);
            if (_elementToMarkup.TryGetValue(previouslySelected, out var markup))
            {
                markup.UpdateVisual(false);
            }
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
                border.Background = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0));
                border.BorderBrush = Brushes.DimGray;
                border.BorderThickness = new Thickness(1);
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

        private Point ClampToCanvas(Point point)
        {
            var x = System.Math.Max(0, System.Math.Min(OverlayCanvas.ActualWidth, point.X));
            var y = System.Math.Max(0, System.Math.Min(OverlayCanvas.ActualHeight, point.Y));
            return new Point(x, y);
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
