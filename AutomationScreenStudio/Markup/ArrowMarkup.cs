using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutomationScreenStudio.Markup
{
    public sealed class ArrowMarkup : LineMarkup
    {
        public ArrowMarkup(Color strokeColor, double thickness, Point start, Point end)
            : base(strokeColor, thickness, start, end)
        {
        }

        public override void Draw(DrawingContext drawingContext)
        {
            var geometry = BuildGeometry();
            var pen = new Pen(CreateStrokeBrush(), Thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };

            pen.Freeze();
            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
        }

        protected override FrameworkElement CreateVisual()
        {
            return new Path();
        }

        protected override void UpdateVisualCore(FrameworkElement element, bool isSelected)
        {
            var path = (Path)element;
            path.Data = BuildGeometry();
            path.Stroke = CreateStrokeBrush();
            path.StrokeThickness = Thickness;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            ApplySelectionStyle(path, isSelected);
        }

        private Geometry BuildGeometry()
        {
            var vector = End - Start;
            if (vector.Length < double.Epsilon)
            {
                return Geometry.Empty;
            }

            vector.Normalize();
            var normal = new Vector(-vector.Y, vector.X);
            var headLength = System.Math.Max(12, Thickness * 4);
            var headWidth = System.Math.Max(8, Thickness * 3);

            var headBase = End - (vector * headLength);
            var left = headBase + (normal * (headWidth / 2));
            var right = headBase - (normal * (headWidth / 2));

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(Start, false, false);
                context.LineTo(End, true, false);
                context.BeginFigure(left, false, false);
                context.LineTo(End, true, false);
                context.LineTo(right, true, false);
            }

            return geometry;
        }
    }
}
