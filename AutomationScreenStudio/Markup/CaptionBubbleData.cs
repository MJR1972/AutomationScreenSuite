using System.Windows.Media;
using AutomationScreenStudio.Enums;

namespace AutomationScreenStudio.Markup
{
    internal sealed class CaptionBubbleData
    {
        public CaptionBubbleStyle Style { get; set; }

        public TailSide TailSide { get; set; }

        public Color StrokeColor { get; set; }

        public double StrokeThickness { get; set; }

        public Color FillColor { get; set; } = Colors.White;

        public string Text { get; set; } = string.Empty;

        public double FontSize { get; set; }

        public bool IsPlaceholderVisible { get; set; }
    }
}
