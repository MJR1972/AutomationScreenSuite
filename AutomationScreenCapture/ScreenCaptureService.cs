using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AutomationScreenCapture
{
    public sealed class ScreenCaptureService
    {
        public async Task<CaptureResult> CaptureRegionAsync(CaptureOptions? options = null)
        {
            options ??= new CaptureOptions();
            var selection = await ShowOverlayAsync(options).ConfigureAwait(false);
            if (!selection.HasValue)
            {
                return new CaptureResult
                {
                    IsCanceled = true,
                    SelectedRegionVirtualPixels = Int32Rect.Empty
                };
            }

            var captureResult = DesktopCapture.CaptureRegion(selection.Value, options.IncludeCursor);
            if (options.CopyToClipboard && !captureResult.IsCanceled && captureResult.Image != null)
            {
                await RunStaActionAsync(() => Clipboard.SetImage(captureResult.Image)).ConfigureAwait(false);
            }

            return captureResult;
        }

        private static Task<Int32Rect?> ShowOverlayAsync(CaptureOptions options)
        {
            var completionSource = new TaskCompletionSource<Int32Rect?>();
            var thread = new Thread(() =>
            {
                try
                {
                    var window = new SelectionOverlayWindow(options.DimOpacity);
                    window.ShowDialog();
                    completionSource.SetResult(window.SelectionResult);
                }
                catch (Exception exception)
                {
                    completionSource.SetException(exception);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return completionSource.Task;
        }

        private static Task RunStaActionAsync(Action action)
        {
            var completionSource = new TaskCompletionSource<object?>();
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    completionSource.SetResult(null);
                }
                catch (Exception exception)
                {
                    completionSource.SetException(exception);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return completionSource.Task;
        }
    }
}
