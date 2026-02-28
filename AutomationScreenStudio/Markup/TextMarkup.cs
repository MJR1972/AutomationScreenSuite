using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AutomationScreenStudio.Markup
{
    public sealed class TextMarkup : MarkupBase
    {
        public TextMarkup(Color strokeColor, double thickness, Point location, string text)
            : base(strokeColor, thickness)
        {
            Location = location;
            Text = text;
        }

        public Point Location { get; set; }

        public string Text { get; set; }

        public double FontSize => 12 + (Thickness * 1.5);

        public override bool HitTest(Point point)
        {
            return GetBounds().Contains(point);
        }

        public override void Offset(Vector delta)
        {
            Location += delta;
        }

        public override void Draw(DrawingContext drawingContext)
        {
            var bounds = GetBounds();
            var backgroundBrush = Brushes.White;
            var borderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            borderBrush.Freeze();

            drawingContext.DrawRoundedRectangle(
                backgroundBrush,
                new Pen(borderBrush, 1),
                bounds,
                8,
                8);
            drawingContext.DrawText(CreateFormattedText(), new Point(Location.X + 6, Location.Y + 6));
        }

        public override bool IsEmpty()
        {
            return string.IsNullOrWhiteSpace(Text);
        }

        protected override FrameworkElement CreateVisual()
        {
            var textBlock = new TextBlock();
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6),
                Child = textBlock
            };
        }

        protected override void UpdateVisualCore(FrameworkElement element, bool isSelected)
        {
            var border = (Border)element;
            var textBlock = (TextBlock)border.Child;
            textBlock.Text = Text;
            textBlock.Foreground = CreateStrokeBrush();
            textBlock.FontSize = FontSize;
            textBlock.FontFamily = new FontFamily("Segoe UI");
            textBlock.FontWeight = FontWeights.SemiBold;
            Canvas.SetLeft(border, Location.X);
            Canvas.SetTop(border, Location.Y);
            border.Background = Brushes.White;
            border.BorderBrush = isSelected
                ? Brushes.Black
                : new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            border.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            border.CornerRadius = new CornerRadius(8);
            border.Padding = new Thickness(6);
        }

        private Rect GetBounds()
        {
            var formattedText = CreateFormattedText();
            return new Rect(
                Location,
                new Size(
                    formattedText.WidthIncludingTrailingWhitespace + 12,
                    formattedText.Height + 12));
        }

        private FormattedText CreateFormattedText()
        {
            return new FormattedText(
                Text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                FontSize,
                CreateStrokeBrush(),
                1.0);
        }
    }
}
