using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AutomationScreenStudio.Enums;

namespace AutomationScreenStudio.Markup
{
    public sealed class CaptionBubbleMarkup : MarkupBase
    {
        private const double PaddingSize = 10;
        private const double TailHeight = 18;
        private const string PlaceholderText = "Double-click to edit";

        public CaptionBubbleMarkup(
            Color strokeColor,
            double thickness,
            Point start,
            Point end,
            CaptionBubbleStyle bubbleStyle,
            TailSide tailSide)
            : base(strokeColor, thickness)
        {
            Start = start;
            End = end;
            BubbleStyle = bubbleStyle;
            TailSide = tailSide;
            Text = string.Empty;
            IsPlaceholderVisible = true;
        }

        public Point Start { get; set; }

        public Point End { get; set; }

        public CaptionBubbleStyle BubbleStyle { get; set; }

        public TailSide TailSide { get; set; }

        public string Text { get; set; }

        public double FontSize => 12 + (Thickness * 1.5);

        public bool IsPlaceholderVisible { get; set; }

        public override bool HitTest(Point point)
        {
            return GetBounds().Contains(point);
        }

        public override void Offset(Vector delta)
        {
            Start += delta;
            End += delta;
        }

        public override void Draw(DrawingContext drawingContext)
        {
            var bounds = GetBounds();
            var bodyRect = GetBodyRect(bounds);
            var borderBrush = CreateBubbleBorderBrush();
            var pen = new Pen(borderBrush, 1);
            pen.Freeze();

            DrawBubbleBody(drawingContext, bodyRect, pen);
            DrawTail(drawingContext, bounds, bodyRect, pen);
            DrawText(drawingContext, bounds);
        }

        public override bool IsEmpty()
        {
            var bounds = GetBounds();
            return bounds.Width < 12 || bounds.Height < 12;
        }

        public Rect GetTextBounds()
        {
            var bounds = GetBounds();
            var bodyRect = GetBodyRect(bounds);
            return new Rect(
                bodyRect.Left + PaddingSize,
                bodyRect.Top + PaddingSize,
                System.Math.Max(20, bodyRect.Width - (PaddingSize * 2)),
                System.Math.Max(20, bodyRect.Height - (PaddingSize * 2)));
        }

        protected override FrameworkElement CreateVisual()
        {
            return new Border
            {
                Background = Brushes.Transparent
            };
        }

        protected override void UpdateVisualCore(FrameworkElement element, bool isSelected)
        {
            var root = (Border)element;
            var canvas = root.Child as Canvas;
            if (canvas == null)
            {
                canvas = new Canvas();
                root.Child = canvas;
            }

            var bounds = GetBounds();
            var bodyRect = GetBodyRect(bounds);
            var localBodyRect = new Rect(0, 0, bodyRect.Width, bodyRect.Height);
            var localBounds = new Rect(0, 0, bounds.Width, bounds.Height);

            root.Width = bounds.Width;
            root.Height = bounds.Height;
            Canvas.SetLeft(root, bounds.Left);
            Canvas.SetTop(root, bounds.Top);
            root.Tag = new CaptionBubbleData
            {
                Style = BubbleStyle,
                TailSide = TailSide,
                StrokeColor = StrokeColor,
                StrokeThickness = Thickness,
                FillColor = Colors.White,
                Text = Text,
                FontSize = FontSize,
                IsPlaceholderVisible = IsPlaceholderVisible
            };

            canvas.Children.Clear();

            AddBodyVisual(canvas, localBodyRect);
            AddTailVisual(canvas, localBounds, localBodyRect);
            AddTextVisual(canvas, localBounds);
        }

        private void AddBodyVisual(Canvas canvas, Rect bodyRect)
        {
            var bodyBorder = new Border
            {
                Width = bodyRect.Width,
                Height = bodyRect.Height,
                Background = Brushes.White,
                BorderBrush = CreateBubbleBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = GetCornerRadius(bodyRect)
            };

            Canvas.SetLeft(bodyBorder, bodyRect.Left);
            Canvas.SetTop(bodyBorder, bodyRect.Top);
            canvas.Children.Add(bodyBorder);
        }

        private void AddTailVisual(Canvas canvas, Rect bounds, Rect bodyRect)
        {
            if (BubbleStyle == CaptionBubbleStyle.Thought)
            {
                var circles = BuildThoughtTailCircles(bounds, bodyRect);
                foreach (var circle in circles)
                {
                    canvas.Children.Add(circle);
                }

                return;
            }

            var tail = new Polygon
            {
                Fill = Brushes.White,
                Stroke = CreateBubbleBorderBrush(),
                StrokeThickness = 1,
                Points = BuildTailPoints(bounds, bodyRect)
            };

            canvas.Children.Add(tail);
        }

        private void AddTextVisual(Canvas canvas, Rect bounds)
        {
            if (string.IsNullOrWhiteSpace(Text) && !IsPlaceholderVisible)
            {
                return;
            }

            var localTextBounds = new Rect(
                PaddingSize,
                PaddingSize,
                System.Math.Max(20, bounds.Width - (PaddingSize * 2)),
                System.Math.Max(20, GetBodyRect(bounds).Height - (PaddingSize * 2)));

            var textBlock = new TextBlock
            {
                Text = IsPlaceholderVisible ? PlaceholderText : Text,
                Width = localTextBounds.Width,
                TextWrapping = TextWrapping.Wrap,
                Foreground = IsPlaceholderVisible ? CreatePlaceholderBrush() : CreateStrokeBrush(),
                FontSize = FontSize,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold
            };

            Canvas.SetLeft(textBlock, localTextBounds.Left);
            Canvas.SetTop(textBlock, localTextBounds.Top);
            canvas.Children.Add(textBlock);
        }

        private void DrawBubbleBody(DrawingContext drawingContext, Rect bodyRect, Pen pen)
        {
            var cornerRadius = GetCornerRadius(bodyRect);
            drawingContext.DrawRoundedRectangle(Brushes.White, pen, bodyRect, cornerRadius.TopLeft, cornerRadius.TopLeft);
        }

        private void DrawTail(DrawingContext drawingContext, Rect bounds, Rect bodyRect, Pen pen)
        {
            var localBounds = new Rect(0, 0, bounds.Width, bounds.Height);
            var localBodyRect = new Rect(0, 0, bodyRect.Width, bodyRect.Height);

            if (BubbleStyle == CaptionBubbleStyle.Thought)
            {
                foreach (var circle in BuildThoughtTailCircles(localBounds, localBodyRect))
                {
                    var left = Canvas.GetLeft(circle);
                    var top = Canvas.GetTop(circle);
                    drawingContext.DrawEllipse(
                        circle.Fill,
                        new Pen(circle.Stroke, circle.StrokeThickness),
                        new Point(bounds.Left + left + (circle.Width / 2), bounds.Top + top + (circle.Height / 2)),
                        circle.Width / 2,
                        circle.Height / 2);
                }

                return;
            }

            var tailGeometry = new StreamGeometry();
            using (var context = tailGeometry.Open())
            {
                var points = BuildTailPoints(localBounds, localBodyRect);
                context.BeginFigure(new Point(bounds.Left + points[0].X, bounds.Top + points[0].Y), true, true);
                context.LineTo(new Point(bounds.Left + points[1].X, bounds.Top + points[1].Y), true, false);
                context.LineTo(new Point(bounds.Left + points[2].X, bounds.Top + points[2].Y), true, false);
            }

            tailGeometry.Freeze();
            drawingContext.DrawGeometry(Brushes.White, pen, tailGeometry);
        }

        private void DrawText(DrawingContext drawingContext, Rect bounds)
        {
            if (string.IsNullOrWhiteSpace(Text) && !IsPlaceholderVisible)
            {
                return;
            }

            var textBounds = GetTextBounds();
            var formattedText = new FormattedText(
                IsPlaceholderVisible ? PlaceholderText : Text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                FontSize,
                IsPlaceholderVisible ? CreatePlaceholderBrush() : CreateStrokeBrush(),
                1.0)
            {
                MaxTextWidth = textBounds.Width,
                MaxTextHeight = textBounds.Height,
                Trimming = TextTrimming.None
            };

            drawingContext.DrawText(formattedText, new Point(textBounds.Left, textBounds.Top));
        }

        private Rect GetBounds()
        {
            var normalized = NormalizeRect(Start, End);
            return new Rect(
                normalized.Left,
                normalized.Top,
                System.Math.Max(70, normalized.Width),
                System.Math.Max(50, normalized.Height));
        }

        private Rect GetBodyRect(Rect bounds)
        {
            var bodyHeight = System.Math.Max(30, bounds.Height - TailHeight);
            return new Rect(bounds.Left, bounds.Top, bounds.Width, bodyHeight);
        }

        private PointCollection BuildTailPoints(Rect bounds, Rect bodyRect)
        {
            var baseCenterX = TailSide == TailSide.Left
                ? System.Math.Max(18, bodyRect.Width * 0.25)
                : System.Math.Min(bodyRect.Width - 18, bodyRect.Width * 0.75);

            var baseHalfWidth = System.Math.Min(16, System.Math.Max(10, bodyRect.Width * 0.08));
            var tipX = TailSide == TailSide.Left
                ? System.Math.Max(10, baseCenterX - 14)
                : System.Math.Min(bounds.Width - 10, baseCenterX + 14);

            return new PointCollection
            {
                new Point(baseCenterX - baseHalfWidth, bodyRect.Bottom - bounds.Top),
                new Point(baseCenterX + baseHalfWidth, bodyRect.Bottom - bounds.Top),
                new Point(tipX, bounds.Height - 2)
            };
        }

        private Ellipse[] BuildThoughtTailCircles(Rect bounds, Rect bodyRect)
        {
            if (TailSide == TailSide.Left)
            {
                return new[]
                {
                    CreateTailCircle(18, bounds.Height - 24, 12),
                    CreateTailCircle(10, bounds.Height - 14, 8),
                    CreateTailCircle(4, bounds.Height - 7, 5)
                };
            }

            return new[]
            {
                CreateTailCircle(bounds.Width - 30, bounds.Height - 24, 12),
                CreateTailCircle(bounds.Width - 18, bounds.Height - 14, 8),
                CreateTailCircle(bounds.Width - 9, bounds.Height - 7, 5)
            };
        }

        private Ellipse CreateTailCircle(double left, double top, double size)
        {
            var ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = CreateBubbleBorderBrush(),
                StrokeThickness = 1
            };

            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);
            return ellipse;
        }

        private CornerRadius GetCornerRadius(Rect bodyRect)
        {
            switch (BubbleStyle)
            {
                case CaptionBubbleStyle.SquareCallout:
                    return new CornerRadius(4);
                case CaptionBubbleStyle.Thought:
                    return new CornerRadius(System.Math.Min(bodyRect.Height / 2, 18));
                default:
                    return new CornerRadius(12);
            }
        }

        private Brush CreateBubbleBorderBrush()
        {
            var brush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            brush.Freeze();
            return brush;
        }

        private Brush CreatePlaceholderBrush()
        {
            var brush = new SolidColorBrush(Colors.LightGray);
            brush.Freeze();
            return brush;
        }
    }
}
