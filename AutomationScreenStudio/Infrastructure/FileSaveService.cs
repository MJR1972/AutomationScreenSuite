using System;
using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace AutomationScreenStudio.Infrastructure
{
    public sealed class FileSaveService : IFileSaveService
    {
        public string? SaveBitmap(BitmapSource image)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|JPEG Image (*.jpeg)|*.jpeg",
                DefaultExt = ".png",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() != true)
            {
                return null;
            }

            BitmapEncoder encoder;
            var extension = Path.GetExtension(dialog.FileName);
            if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                encoder = new JpegBitmapEncoder { QualityLevel = 95 };
            }
            else
            {
                encoder = new PngBitmapEncoder();
            }

            encoder.Frames.Add(BitmapFrame.Create(image));

            using (var stream = File.Create(dialog.FileName))
            {
                encoder.Save(stream);
            }

            return dialog.FileName;
        }
    }
}
