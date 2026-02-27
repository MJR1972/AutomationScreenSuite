using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutomationScreenStudio.Markup
{
    public sealed class EllipseMarkup : MarkupBase
    {
        public EllipseMarkup(Color strokeColor, double thickness, Point start, Point end)
            : base(strokeColor, thickness)
        {
            Start = start;
            End = end;
        }

        public Point Start { get; set; }

        public Point End { get; set; }

        public override bool HitTest(Point point)
        {
            var bounds = NormalizeRect(Start, End);
            var inflated = bounds;
            inflated.Inflate(System.Math.Max(6, Thickness + 2), System.Math.Max(6, Thickness + 2));
            return inflated.Contains(point);
        }

        public override void Offset(Vector delta)
        {
            Start += delta;
            End += delta;
        }

        public override void Draw(DrawingContext drawingContext)
        {
            var bounds = NormalizeRect(Start, End);
            var pen = new Pen(CreateStrokeBrush(), Thickness);
            pen.Freeze();
            drawingContext.DrawEllipse(
                null,
                pen,
                new Point(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2)),
                bounds.Width / 2,
                bounds.Height / 2);
        }

        public override bool IsEmpty()
        {
            var rect = NormalizeRect(Start, End);
            return rect.Width < 2 || rect.Height < 2;
        }

        protected override FrameworkElement CreateVisual()
        {
            return new Ellipse();
        }

        protected override void UpdateVisualCore(FrameworkElement element, bool isSelected)
        {
            var ellipse = (Ellipse)element;
            var rect = NormalizeRect(Start, End);
            Canvas.SetLeft(ellipse, rect.Left);
            Canvas.SetTop(ellipse, rect.Top);
            ellipse.Width = rect.Width;
            ellipse.Height = rect.Height;
            ellipse.Stroke = CreateStrokeBrush();
            ellipse.StrokeThickness = Thickness;
            ellipse.Fill = Brushes.Transparent;
            ApplySelectionStyle(ellipse, isSelected);
        }
    }
}
