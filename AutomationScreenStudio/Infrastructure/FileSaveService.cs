using System;
using System.IO;
using System.Windows.Media.Imaging;
using AutomationScreenCapture.Imaging;
using Microsoft.Win32;

namespace AutomationScreenStudio.Infrastructure
{
    public sealed class FileSaveService : IFileSaveService
    {
        public FileSaveResult? SaveBitmap(BitmapSource image, ImageExportOptions options)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var defaultExtension = options.Format == ImageExportFormat.Jpeg ? ".jpg" : ".png";
            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|JPEG Image (*.jpeg)|*.jpeg",
                DefaultExt = defaultExtension,
                AddExtension = true,
                OverwritePrompt = true,
                FileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() != true)
            {
                return null;
            }

            var selectedOptions = new ImageExportOptions
            {
                Format = GetFormatFromFileName(dialog.FileName, options.Format),
                JpegQuality = options.JpegQuality,
                RemoveAlphaIfOpaque = options.RemoveAlphaIfOpaque,
                ConvertToBgr24WhenPossible = options.ConvertToBgr24WhenPossible
            };

            var bytes = ImageExportService.Encode(image, selectedOptions);

            using (var stream = File.Create(dialog.FileName))
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            return new FileSaveResult
            {
                FilePath = dialog.FileName,
                BytesWritten = bytes.LongLength
            };
        }

        private static ImageExportFormat GetFormatFromFileName(string filePath, ImageExportFormat fallback)
        {
            var extension = Path.GetExtension(filePath);
            if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return ImageExportFormat.Jpeg;
            }

            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return ImageExportFormat.Png;
            }

            return fallback;
        }
    }
}
