using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AutomationScreenCapture
{
    internal sealed class SelectionOverlayWindow : Window
    {
        private readonly Int32Rect _virtualScreenBounds;
        private readonly Canvas _rootCanvas;
        private readonly Path _dimPath;
        private readonly Rectangle _selectionBorder;
        private bool _isDragging;
        private Point _dragStartDip;
        private Matrix _dipToDevice = Matrix.Identity;
        private Matrix _deviceToDip = Matrix.Identity;

        internal SelectionOverlayWindow(byte dimOpacity)
        {
            _virtualScreenBounds = DesktopCapture.GetVirtualScreenBounds();
            Cursor = Cursors.Cross;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Focusable = true;
            WindowStartupLocation = WindowStartupLocation.Manual;

            _rootCanvas = new Canvas
            {
                Background = Brushes.Transparent
            };

            _dimPath = new Path
            {
                Fill = new SolidColorBrush(Color.FromArgb(dimOpacity, 0, 0, 0))
            };

            _selectionBorder = new Rectangle
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                Visibility = Visibility.Collapsed
            };

            _rootCanvas.Children.Add(_dimPath);
            _rootCanvas.Children.Add(_selectionBorder);
            Content = _rootCanvas;

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            PreviewKeyDown += OnPreviewKeyDown;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        internal Int32Rect? SelectionResult { get; private set; }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null)
            {
                return;
            }

            _deviceToDip = source.CompositionTarget.TransformFromDevice;
            _dipToDevice = source.CompositionTarget.TransformToDevice;

            var topLeft = _deviceToDip.Transform(new Point(_virtualScreenBounds.X, _virtualScreenBounds.Y));
            var bottomRight = _deviceToDip.Transform(new Point(
                _virtualScreenBounds.X + _virtualScreenBounds.Width,
                _virtualScreenBounds.Y + _virtualScreenBounds.Height));

            Left = topLeft.X;
            Top = topLeft.Y;
            Width = bottomRight.X - topLeft.X;
            Height = bottomRight.Y - topLeft.Y;
            UpdateDimGeometry(Rect.Empty);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Focus();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SelectionResult = null;
                DialogResult = false;
                Close();
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartDip = e.GetPosition(_rootCanvas);
            _isDragging = true;
            CaptureMouse();
            _selectionBorder.Visibility = Visibility.Visible;
            UpdateSelectionVisual(_dragStartDip, _dragStartDip);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdateSelectionVisual(_dragStartDip, e.GetPosition(_rootCanvas));
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            ReleaseMouseCapture();

            var selectionRectDip = NormalizeRect(_dragStartDip, e.GetPosition(_rootCanvas));
            var selectionRectPixels = ToVirtualPixelRect(selectionRectDip);

            if (selectionRectPixels.Width <= 0 || selectionRectPixels.Height <= 0)
            {
                SelectionResult = null;
                DialogResult = false;
            }
            else
            {
                SelectionResult = selectionRectPixels;
                DialogResult = true;
            }

            Close();
        }

        private void UpdateSelectionVisual(Point startDip, Point endDip)
        {
            var rect = NormalizeRect(startDip, endDip);
            Canvas.SetLeft(_selectionBorder, rect.X);
            Canvas.SetTop(_selectionBorder, rect.Y);
            _selectionBorder.Width = rect.Width;
            _selectionBorder.Height = rect.Height;
            UpdateDimGeometry(rect);
        }

        private void UpdateDimGeometry(Rect selectionRect)
        {
            var geometry = new PathGeometry
            {
                FillRule = FillRule.EvenOdd
            };

            geometry.Figures.Add(new PathFigure
            {
                StartPoint = new Point(0, 0),
                Segments = new PathSegmentCollection
                {
                    new LineSegment(new Point(Width, 0), true),
                    new LineSegment(new Point(Width, Height), true),
                    new LineSegment(new Point(0, Height), true)
                },
                IsClosed = true
            });

            if (!selectionRect.IsEmpty && selectionRect.Width > 0 && selectionRect.Height > 0)
            {
                geometry.Figures.Add(new PathFigure
                {
                    StartPoint = new Point(selectionRect.Left, selectionRect.Top),
                    Segments = new PathSegmentCollection
                    {
                        new LineSegment(new Point(selectionRect.Right, selectionRect.Top), true),
                        new LineSegment(new Point(selectionRect.Right, selectionRect.Bottom), true),
                        new LineSegment(new Point(selectionRect.Left, selectionRect.Bottom), true)
                    },
                    IsClosed = true
                });
            }

            _dimPath.Data = geometry;
        }

        private Int32Rect ToVirtualPixelRect(Rect dipRect)
        {
            var topLeft = _dipToDevice.Transform(new Point(dipRect.Left, dipRect.Top));
            var bottomRight = _dipToDevice.Transform(new Point(dipRect.Right, dipRect.Bottom));

            var x = _virtualScreenBounds.X + (int)Math.Round(topLeft.X);
            var y = _virtualScreenBounds.Y + (int)Math.Round(topLeft.Y);
            var width = Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X));
            var height = Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y));

            return new Int32Rect(x, y, width, height);
        }

        private static Rect NormalizeRect(Point first, Point second)
        {
            return new Rect(
                Math.Min(first.X, second.X),
                Math.Min(first.Y, second.Y),
                Math.Abs(first.X - second.X),
                Math.Abs(first.Y - second.Y));
        }
    }
}
