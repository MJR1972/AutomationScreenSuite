namespace AutomationScreenStudio.ViewModels
{
    public sealed class ZoomPreset
    {
        public ZoomPreset(string displayText, double value)
        {
            DisplayText = displayText;
            Value = value;
        }

        public string DisplayText { get; }

        public double Value { get; }
    }
}
