using System.Windows.Media;

namespace AutomationScreenStudio.Markup
{
    public sealed class ColorOption
    {
        public ColorOption(string name, Color color)
        {
            Name = name;
            Color = color;
        }

        public string Name { get; }

        public Color Color { get; }
    }
}
