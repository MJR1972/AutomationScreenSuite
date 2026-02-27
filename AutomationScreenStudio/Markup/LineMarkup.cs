using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutomationScreenStudio.Markup
{
    public class LineMarkup : MarkupBase
    {
        public LineMarkup(Color strokeColor, double thickness, Point start, Point end)
            : base(strokeColor, thickness)
        {
            Start = start;
            End = end;
        }

        public Point Start { get; set; }

        public Point End { get; set; }

        public override bool HitTest(Point point)
        {
            return DistanceToSegment(point, Start, End) <= System.Math.Max(6, Thickness + 3);
        }

        public override void Offset(Vector delta)
        {
            Start += delta;
            End += delta;
        }

        public override void Draw(DrawingContext drawingContext)
        {
            var pen = new Pen(CreateStrokeBrush(), Thickness);
            pen.Freeze();
            drawingContext.DrawLine(pen, Start, End);
        }

        public override bool IsEmpty()
        {
            return (End - Start).Length < 2;
        }

        protected override FrameworkElement CreateVisual()
        {
            return new Line();
        }

        protected override void UpdateVisualCore(FrameworkElement element, bool isSelected)
        {
            var line = (Line)element;
            line.X1 = Start.X;
            line.Y1 = Start.Y;
            line.X2 = End.X;
            line.Y2 = End.Y;
            line.Stroke = CreateStrokeBrush();
            line.StrokeThickness = Thickness;
            ApplySelectionStyle(line, isSelected);
        }
    }
}
