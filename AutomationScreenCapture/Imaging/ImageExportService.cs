using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutomationScreenCapture.Imaging
{
    public static class ImageExportService
    {
        public static byte[] Encode(BitmapSource source, ImageExportOptions options)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var preparedSource = PrepareForEncoding(source, options);
            BitmapEncoder encoder;
            switch (options.Format)
            {
                case ImageExportFormat.Jpeg:
                    encoder = new JpegBitmapEncoder
                    {
                        QualityLevel = ClampJpegQuality(options.JpegQuality)
                    };
                    break;
                default:
                    encoder = new PngBitmapEncoder();
                    break;
            }

            encoder.Frames.Add(BitmapFrame.Create(preparedSource));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        public static long GetEncodedSizeBytes(BitmapSource source, ImageExportOptions options)
        {
            return Encode(source, options).LongLength;
        }

        private static BitmapSource PrepareForEncoding(BitmapSource source, ImageExportOptions options)
        {
            var normalized = NormalizeToBgra32(source);
            var shouldUseBgr24 = options.ConvertToBgr24WhenPossible &&
                                 (options.Format == ImageExportFormat.Jpeg ||
                                  (options.RemoveAlphaIfOpaque && IsFullyOpaque(normalized)));

            if (!shouldUseBgr24)
            {
                return normalized;
            }

            return ConvertToFormat(normalized, PixelFormats.Bgr24);
        }

        private static bool IsFullyOpaque(BitmapSource source)
        {
            if (!HasAlphaChannel(source.Format))
            {
                return true;
            }

            var normalized = NormalizeToBgra32(source);
            var stride = normalized.PixelWidth * 4;
            var pixels = new byte[stride * normalized.PixelHeight];
            normalized.CopyPixels(pixels, stride, 0);

            for (var index = 3; index < pixels.Length; index += 4)
            {
                if (pixels[index] != byte.MaxValue)
                {
                    return false;
                }
            }

            return true;
        }

        private static BitmapSource NormalizeToBgra32(BitmapSource source)
        {
            if (source.Format == PixelFormats.Bgra32)
            {
                return EnsureFrozen(source);
            }

            return ConvertToFormat(source, PixelFormats.Bgra32);
        }

        private static BitmapSource ConvertToFormat(BitmapSource source, System.Windows.Media.PixelFormat format)
        {
            var converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = source;
            converted.DestinationFormat = format;
            converted.EndInit();
            converted.Freeze();
            return converted;
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

        private static int ClampJpegQuality(int quality)
        {
            return Math.Max(1, Math.Min(100, quality));
        }

        private static bool HasAlphaChannel(PixelFormat format)
        {
            return format == PixelFormats.Bgra32 ||
                   format == PixelFormats.Pbgra32 ||
                   format == PixelFormats.Prgba64 ||
                   format == PixelFormats.Rgba128Float ||
                   format == PixelFormats.Rgba64;
        }
    }
}
