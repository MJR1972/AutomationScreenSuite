using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutomationScreenStudio.Markup
{
    public abstract class MarkupBase
    {
        protected MarkupBase(Color strokeColor, double thickness)
        {
            StrokeColor = strokeColor;
            Thickness = thickness;
        }

        public Color StrokeColor { get; private set; }

        public double Thickness { get; private set; }

        public FrameworkElement? VisualElement { get; private set; }

        public FrameworkElement GetOrCreateVisual()
        {
            if (VisualElement == null)
            {
                VisualElement = CreateVisual();
            }

            UpdateVisual(false);
            return VisualElement;
        }

        public void UpdateVisual(bool isSelected)
        {
            if (VisualElement != null)
            {
                UpdateVisualCore(VisualElement, isSelected);
            }
        }

        public abstract bool HitTest(Point point);

        public abstract void Offset(Vector delta);

        public abstract void Draw(DrawingContext drawingContext);

        public abstract bool IsEmpty();

        public void SetStyle(Color strokeColor, double thickness)
        {
            StrokeColor = strokeColor;
            Thickness = thickness;
        }

        protected Brush CreateStrokeBrush()
        {
            var brush = new SolidColorBrush(StrokeColor);
            brush.Freeze();
            return brush;
        }

        protected static Rect NormalizeRect(Point first, Point second)
        {
            return new Rect(
                Math.Min(first.X, second.X),
                Math.Min(first.Y, second.Y),
                Math.Abs(first.X - second.X),
                Math.Abs(first.Y - second.Y));
        }

        protected abstract FrameworkElement CreateVisual();

        protected abstract void UpdateVisualCore(FrameworkElement element, bool isSelected);

        protected static void ApplySelectionStyle(Shape shape, bool isSelected)
        {
            shape.StrokeDashArray = isSelected ? new DoubleCollection { 6, 3 } : null;
        }

        protected static double DistanceToSegment(Point point, Point start, Point end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                return (point - start).Length;
            }

            var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / ((dx * dx) + (dy * dy));
            t = Math.Max(0, Math.Min(1, t));

            var projection = new Point(start.X + (t * dx), start.Y + (t * dy));
            return (point - projection).Length;
        }
    }
}
