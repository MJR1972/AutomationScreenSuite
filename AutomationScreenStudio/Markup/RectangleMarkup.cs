using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutomationScreenStudio.Markup
{
    public sealed class RectangleMarkup : MarkupBase
    {
        public RectangleMarkup(Color strokeColor, double thickness, Point start, Point end, bool isRounded)
            : base(strokeColor, thickness)
        {
            Start = start;
            End = end;
            IsRounded = isRounded;
        }

        public Point Start { get; set; }

        public Point End { get; set; }

        public bool IsRounded { get; }

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
            var rect = NormalizeRect(Start, End);
            var pen = new Pen(CreateStrokeBrush(), Thickness);
            pen.Freeze();

            var radius = IsRounded ? System.Math.Max(8, Thickness * 2) : 0;
            drawingContext.DrawRoundedRectangle(null, pen, rect, radius, radius);
        }

        public override bool IsEmpty()
        {
            var rect = NormalizeRect(Start, End);
            return rect.Width < 2 || rect.Height < 2;
        }

        protected override FrameworkElement CreateVisual()
        {
            return new Rectangle();
        }

        protected override void UpdateVisualCore(FrameworkElement element, bool isSelected)
        {
            var rectangle = (Rectangle)element;
            var rect = NormalizeRect(Start, End);
            Canvas.SetLeft(rectangle, rect.Left);
            Canvas.SetTop(rectangle, rect.Top);
            rectangle.Width = rect.Width;
            rectangle.Height = rect.Height;
            rectangle.Stroke = CreateStrokeBrush();
            rectangle.StrokeThickness = Thickness;
            rectangle.Fill = Brushes.Transparent;
            rectangle.RadiusX = IsRounded ? System.Math.Max(8, Thickness * 2) : 0;
            rectangle.RadiusY = rectangle.RadiusX;
            ApplySelectionStyle(rectangle, isSelected);
        }
    }
}
