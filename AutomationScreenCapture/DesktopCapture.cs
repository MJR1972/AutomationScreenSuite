using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AutomationScreenCapture
{
    internal static class DesktopCapture
    {
        internal static CaptureResult CaptureRegion(Int32Rect selectedRegionVirtualPixels, bool includeCursor)
        {
            var virtualBounds = GetVirtualScreenBounds();
            var normalizedSelection = NormalizeSelection(selectedRegionVirtualPixels, virtualBounds);
            if (normalizedSelection.Width <= 0 || normalizedSelection.Height <= 0)
            {
                return new CaptureResult
                {
                    IsCanceled = true,
                    SelectedRegionVirtualPixels = Int32Rect.Empty
                };
            }

            var fullScreen = CaptureVirtualScreenBitmap(virtualBounds, includeCursor);
            var cropRegion = new Int32Rect(
                normalizedSelection.X - virtualBounds.X,
                normalizedSelection.Y - virtualBounds.Y,
                normalizedSelection.Width,
                normalizedSelection.Height);

            var cropped = new CroppedBitmap(fullScreen, cropRegion);
            cropped.Freeze();

            return new CaptureResult
            {
                IsCanceled = false,
                SelectedRegionVirtualPixels = normalizedSelection,
                Image = cropped
            };
        }

        internal static Int32Rect GetVirtualScreenBounds()
        {
            return new Int32Rect(
                NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
                NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
                NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
                NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));
        }

        private static BitmapSource CaptureVirtualScreenBitmap(Int32Rect virtualBounds, bool includeCursor)
        {
            IntPtr screenDc = IntPtr.Zero;
            IntPtr memoryDc = IntPtr.Zero;
            IntPtr bitmapHandle = IntPtr.Zero;
            IntPtr previousObject = IntPtr.Zero;

            try
            {
                screenDc = NativeMethods.GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero)
                {
                    throw new Win32Exception("Unable to acquire the desktop device context.");
                }

                memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
                if (memoryDc == IntPtr.Zero)
                {
                    throw new Win32Exception("Unable to create a compatible device context.");
                }

                bitmapHandle = NativeMethods.CreateCompatibleBitmap(screenDc, virtualBounds.Width, virtualBounds.Height);
                if (bitmapHandle == IntPtr.Zero)
                {
                    throw new Win32Exception("Unable to create a compatible bitmap.");
                }

                previousObject = NativeMethods.SelectObject(memoryDc, bitmapHandle);
                if (previousObject == IntPtr.Zero)
                {
                    throw new Win32Exception("Unable to select the compatible bitmap.");
                }

                if (!NativeMethods.BitBlt(
                        memoryDc,
                        0,
                        0,
                        virtualBounds.Width,
                        virtualBounds.Height,
                        screenDc,
                        virtualBounds.X,
                        virtualBounds.Y,
                        NativeMethods.SRCCOPY))
                {
                    throw new Win32Exception("BitBlt failed while capturing the desktop.");
                }

                if (includeCursor)
                {
                    DrawCursor(memoryDc, virtualBounds);
                }

                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmapHandle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                if (previousObject != IntPtr.Zero && memoryDc != IntPtr.Zero)
                {
                    NativeMethods.SelectObject(memoryDc, previousObject);
                }

                if (bitmapHandle != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(bitmapHandle);
                }

                if (memoryDc != IntPtr.Zero)
                {
                    NativeMethods.DeleteDC(memoryDc);
                }

                if (screenDc != IntPtr.Zero)
                {
                    NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        private static void DrawCursor(IntPtr targetDc, Int32Rect virtualBounds)
        {
            var cursorInfo = new NativeMethods.CURSORINFO
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.CURSORINFO))
            };

            if (!NativeMethods.GetCursorInfo(out cursorInfo) || (cursorInfo.flags & NativeMethods.CURSOR_SHOWING) == 0)
            {
                return;
            }

            if (!NativeMethods.GetIconInfo(cursorInfo.hCursor, out var iconInfo))
            {
                return;
            }

            try
            {
                var drawX = cursorInfo.ptScreenPos.X - iconInfo.xHotspot - virtualBounds.X;
                var drawY = cursorInfo.ptScreenPos.Y - iconInfo.yHotspot - virtualBounds.Y;

                NativeMethods.DrawIconEx(
                    targetDc,
                    drawX,
                    drawY,
                    cursorInfo.hCursor,
                    0,
                    0,
                    0,
                    IntPtr.Zero,
                    NativeMethods.DI_NORMAL);
            }
            finally
            {
                if (iconInfo.hbmColor != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(iconInfo.hbmColor);
                }

                if (iconInfo.hbmMask != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(iconInfo.hbmMask);
                }
            }
        }

        private static Int32Rect NormalizeSelection(Int32Rect selection, Int32Rect virtualBounds)
        {
            var normalized = new Int32Rect(
                selection.Width >= 0 ? selection.X : selection.X + selection.Width,
                selection.Height >= 0 ? selection.Y : selection.Y + selection.Height,
                Math.Abs(selection.Width),
                Math.Abs(selection.Height));

            var left = Math.Max(normalized.X, virtualBounds.X);
            var top = Math.Max(normalized.Y, virtualBounds.Y);
            var right = Math.Min(normalized.X + normalized.Width, virtualBounds.X + virtualBounds.Width);
            var bottom = Math.Min(normalized.Y + normalized.Height, virtualBounds.Y + virtualBounds.Height);

            if (right <= left || bottom <= top)
            {
                return Int32Rect.Empty;
            }

            return new Int32Rect(left, top, right - left, bottom - top);
        }
    }
}
