using System.Windows.Media;

namespace AutomationScreenCapture.Imaging
{
    public sealed class DeskewOptions
    {
        public double MaxCorrectionDegrees { get; set; } = 45.0;

        public double AngleSearchStepDegrees { get; set; } = 0.5;

        public bool ExpandToFit { get; set; } = true;

        public Color? Background { get; set; } = null;

        public double ConfidenceThreshold { get; set; } = 0.15;
    }
}
