using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutomationScreenCapture.Imaging
{
    public static class ImageProcessingService
    {
        public static BitmapSource ToGrayscale(BitmapSource source)
        {
            var frame = NormalizeToBgra32(source);
            var pixels = CopyPixels(frame, out var stride);

            for (var index = 0; index < pixels.Length; index += 4)
            {
                var blue = pixels[index];
                var green = pixels[index + 1];
                var red = pixels[index + 2];
                var gray = (byte)Math.Max(0, Math.Min(255, Math.Round((0.114 * blue) + (0.587 * green) + (0.299 * red))));

                pixels[index] = gray;
                pixels[index + 1] = gray;
                pixels[index + 2] = gray;
            }

            return CreateBitmap(frame, pixels, stride);
        }

        public static BitmapSource Invert(BitmapSource source)
        {
            var frame = NormalizeToBgra32(source);
            var pixels = CopyPixels(frame, out var stride);

            for (var index = 0; index < pixels.Length; index += 4)
            {
                pixels[index] = (byte)(255 - pixels[index]);
                pixels[index + 1] = (byte)(255 - pixels[index + 1]);
                pixels[index + 2] = (byte)(255 - pixels[index + 2]);
            }

            return CreateBitmap(frame, pixels, stride);
        }

        public static BitmapSource Rotate(BitmapSource source, double degrees, bool expandToFit = true, Color? background = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var normalized = EnsureFrozen(source);
            var width = normalized.PixelWidth;
            var height = normalized.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return normalized;
            }

            var radians = degrees * Math.PI / 180.0;
            var center = new Point(width / 2.0, height / 2.0);

            var minX = 0.0;
            var minY = 0.0;
            double maxX = width;
            double maxY = height;

            if (expandToFit)
            {
                var corners = new[]
                {
                    RotatePoint(new Point(0, 0), center, radians),
                    RotatePoint(new Point(width, 0), center, radians),
                    RotatePoint(new Point(0, height), center, radians),
                    RotatePoint(new Point(width, height), center, radians)
                };

                minX = corners.Min(point => point.X);
                minY = corners.Min(point => point.Y);
                maxX = corners.Max(point => point.X);
                maxY = corners.Max(point => point.Y);
            }

            var renderWidth = Math.Max(1, (int)Math.Ceiling(maxX - minX));
            var renderHeight = Math.Max(1, (int)Math.Ceiling(maxY - minY));

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var fill = new SolidColorBrush(background ?? Colors.Transparent);
                fill.Freeze();
                context.DrawRectangle(fill, null, new Rect(0, 0, renderWidth, renderHeight));

                context.PushTransform(new TranslateTransform(-minX, -minY));
                context.PushTransform(new RotateTransform(degrees, center.X, center.Y));
                context.DrawImage(normalized, new Rect(0, 0, width, height));
                context.Pop();
                context.Pop();
            }

            var target = new RenderTargetBitmap(
                renderWidth,
                renderHeight,
                normalized.DpiX <= 0 ? 96 : normalized.DpiX,
                normalized.DpiY <= 0 ? 96 : normalized.DpiY,
                PixelFormats.Pbgra32);

            target.Render(visual);
            target.Freeze();
            return target;
        }

        public static BitmapSource Rotate90CW(BitmapSource source)
        {
            return Rotate(source, 90);
        }

        public static BitmapSource Rotate90CCW(BitmapSource source)
        {
            return Rotate(source, -90);
        }

        public static BitmapSource Rotate45CW(BitmapSource source)
        {
            return Rotate(source, 45);
        }

        public static BitmapSource Rotate45CCW(BitmapSource source)
        {
            return Rotate(source, -45);
        }

        public static BitmapSource Denoise(BitmapSource source, DenoiseStrength strength)
        {
            var frame = NormalizeToBgra32(source);
            var stride = frame.PixelWidth * 4;
            var pixels = CopyPixels(frame, out _);
            byte[] result;

            switch (strength)
            {
                case DenoiseStrength.Low:
                    result = MedianFilter(pixels, frame.PixelWidth, frame.PixelHeight, stride, 1);
                    break;
                case DenoiseStrength.Medium:
                    result = MedianFilter(pixels, frame.PixelWidth, frame.PixelHeight, stride, 1);
                    result = MedianFilter(result, frame.PixelWidth, frame.PixelHeight, stride, 1);
                    break;
                case DenoiseStrength.High:
                    result = MedianFilter(pixels, frame.PixelWidth, frame.PixelHeight, stride, 2);
                    break;
                default:
                    result = pixels;
                    break;
            }

            return CreateBitmap(frame, result, stride);
        }

        public static DeskewResult Deskew(BitmapSource source, DeskewOptions? options = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            options ??= new DeskewOptions();
            var analysisImage = DownscaleForAnalysis(source, 800);
            var normalized = NormalizeToBgra32(analysisImage);
            var intensities = GetIntensityBuffer(normalized);
            var edges = GetEdgePoints(intensities, normalized.PixelWidth, normalized.PixelHeight);

            if (edges.Count == 0)
            {
                return new DeskewResult
                {
                    Image = EnsureFrozen(source),
                    Applied = false,
                    EstimatedSkewDegrees = 0,
                    Confidence = 0
                };
            }

            var center = new Point(normalized.PixelWidth / 2.0, normalized.PixelHeight / 2.0);
            var scores = new List<(double Angle, double Score)>();
            for (var angle = -options.MaxCorrectionDegrees; angle <= options.MaxCorrectionDegrees + 0.0001; angle += options.AngleSearchStepDegrees)
            {
                scores.Add((angle, ScoreAlignment(edges, center, normalized.PixelHeight, angle)));
            }

            var ordered = scores.OrderByDescending(item => item.Score).ToList();
            var best = ordered[0];
            var average = scores.Average(item => item.Score);
            var secondBest = ordered.Count > 1 ? ordered[1].Score : average;
            var confidence = Math.Max(
                0,
                Math.Max(
                    (best.Score - average) / (Math.Abs(best.Score) + 0.0001),
                    (best.Score - secondBest) / (Math.Abs(best.Score) + 0.0001)));

            if (confidence < options.ConfidenceThreshold)
            {
                return new DeskewResult
                {
                    Image = EnsureFrozen(source),
                    Applied = false,
                    EstimatedSkewDegrees = best.Angle,
                    Confidence = confidence
                };
            }

            return new DeskewResult
            {
                Image = Rotate(source, -best.Angle, options.ExpandToFit, options.Background),
                Applied = true,
                EstimatedSkewDegrees = best.Angle,
                Confidence = confidence
            };
        }

        private static byte[] MedianFilter(byte[] sourcePixels, int width, int height, int stride, int radius)
        {
            var result = new byte[sourcePixels.Length];
            var capacity = (radius * 2 + 1) * (radius * 2 + 1);
            var reds = new byte[capacity];
            var greens = new byte[capacity];
            var blues = new byte[capacity];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var count = 0;
                    for (var ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
                    {
                        for (var nx = Math.Max(0, x - radius); nx <= Math.Min(width - 1, x + radius); nx++)
                        {
                            var offset = (ny * stride) + (nx * 4);
                            blues[count] = sourcePixels[offset];
                            greens[count] = sourcePixels[offset + 1];
                            reds[count] = sourcePixels[offset + 2];
                            count++;
                        }
                    }

                    Array.Sort(blues, 0, count);
                    Array.Sort(greens, 0, count);
                    Array.Sort(reds, 0, count);

                    var targetOffset = (y * stride) + (x * 4);
                    var medianIndex = count / 2;
                    result[targetOffset] = blues[medianIndex];
                    result[targetOffset + 1] = greens[medianIndex];
                    result[targetOffset + 2] = reds[medianIndex];
                    result[targetOffset + 3] = sourcePixels[targetOffset + 3];
                }
            }

            return result;
        }

        private static BitmapSource DownscaleForAnalysis(BitmapSource source, int maxDimension)
        {
            var normalized = EnsureFrozen(source);
            var maxSide = Math.Max(normalized.PixelWidth, normalized.PixelHeight);
            if (maxSide <= maxDimension || maxSide == 0)
            {
                return normalized;
            }

            var scale = maxDimension / (double)maxSide;
            var targetWidth = Math.Max(1, (int)Math.Round(normalized.PixelWidth * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(normalized.PixelHeight * scale));

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawImage(normalized, new Rect(0, 0, targetWidth, targetHeight));
            }

            var target = new RenderTargetBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            return target;
        }

        private static List<Point> GetEdgePoints(byte[] intensities, int width, int height)
        {
            var magnitudes = new double[width * height];
            var magnitudeSum = 0.0;
            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var gx =
                        -intensities[((y - 1) * width) + (x - 1)] + intensities[((y - 1) * width) + (x + 1)] +
                        (-2 * intensities[(y * width) + (x - 1)]) + (2 * intensities[(y * width) + (x + 1)]) +
                        -intensities[((y + 1) * width) + (x - 1)] + intensities[((y + 1) * width) + (x + 1)];

                    var gy =
                        -intensities[((y - 1) * width) + (x - 1)] + (-2 * intensities[((y - 1) * width) + x]) + -intensities[((y - 1) * width) + (x + 1)] +
                        intensities[((y + 1) * width) + (x - 1)] + (2 * intensities[((y + 1) * width) + x]) + intensities[((y + 1) * width) + (x + 1)];

                    var magnitude = Math.Sqrt((gx * gx) + (gy * gy));
                    magnitudes[(y * width) + x] = magnitude;
                    magnitudeSum += magnitude;
                }
            }

            var threshold = magnitudeSum / Math.Max(1, (width - 2) * (height - 2));
            threshold *= 1.5;
            var step = Math.Max(1, Math.Max(width, height) / 300);
            var edgePoints = new List<Point>();

            for (var y = 1; y < height - 1; y += step)
            {
                for (var x = 1; x < width - 1; x += step)
                {
                    if (magnitudes[(y * width) + x] >= threshold)
                    {
                        edgePoints.Add(new Point(x, y));
                    }
                }
            }

            return edgePoints;
        }

        private static double ScoreAlignment(IReadOnlyList<Point> edgePoints, Point center, int height, double skewAngleDegrees)
        {
            var correctionRadians = -skewAngleDegrees * Math.PI / 180.0;
            var cosine = Math.Cos(correctionRadians);
            var sine = Math.Sin(correctionRadians);
            var bins = new int[height + 2];

            foreach (var point in edgePoints)
            {
                var dx = point.X - center.X;
                var dy = point.Y - center.Y;
                var rotatedY = (-dx * sine) + (dy * cosine) + center.Y;
                var bin = (int)Math.Round(rotatedY);
                if (bin >= 0 && bin < bins.Length)
                {
                    bins[bin]++;
                }
            }

            double score = 0;
            foreach (var bin in bins)
            {
                score += bin * bin;
            }

            return score / Math.Max(1, edgePoints.Count);
        }

        private static byte[] GetIntensityBuffer(BitmapSource source)
        {
            var pixels = CopyPixels(source, out _);
            var intensities = new byte[source.PixelWidth * source.PixelHeight];

            for (var y = 0; y < source.PixelHeight; y++)
            {
                for (var x = 0; x < source.PixelWidth; x++)
                {
                    var offset = (y * source.PixelWidth * 4) + (x * 4);
                    intensities[(y * source.PixelWidth) + x] = (byte)Math.Round(
                        (0.114 * pixels[offset]) +
                        (0.587 * pixels[offset + 1]) +
                        (0.299 * pixels[offset + 2]));
                }
            }

            return intensities;
        }

        private static BitmapSource NormalizeToBgra32(BitmapSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Format == PixelFormats.Bgra32)
            {
                return EnsureFrozen(source);
            }

            var converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = source;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            converted.Freeze();
            return converted;
        }

        private static byte[] CopyPixels(BitmapSource source, out int stride)
        {
            stride = source.PixelWidth * 4;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            return pixels;
        }

        private static BitmapSource CreateBitmap(BitmapSource source, byte[] pixels, int stride)
        {
            var bitmap = BitmapSource.Create(
                source.PixelWidth,
                source.PixelHeight,
                source.DpiX <= 0 ? 96 : source.DpiX,
                source.DpiY <= 0 ? 96 : source.DpiY,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);

            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource EnsureFrozen(BitmapSource source)
        {
            if (source.IsFrozen)
            {
                return source;
            }

            if (source.CanFreeze)
            {
                source.Freeze();
            }

            return source;
        }

        private static Point RotatePoint(Point point, Point center, double radians)
        {
            var dx = point.X - center.X;
            var dy = point.Y - center.Y;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);

            return new Point(
                (dx * cosine) - (dy * sine) + center.X,
                (dx * sine) + (dy * cosine) + center.Y);
        }
    }
}
