using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Screen = System.Windows.Forms.Screen;

namespace BiliDesk.Views.TV;

internal static class TvDisplay
{
    private const uint NoZOrderOrActivation = 0x0004 | 0x0010;

    internal static Screen ForWindow(Window window) => Screen.FromHandle(new WindowInteropHelper(window).EnsureHandle());

    internal static Screen Choose(Window desktop, string? remembered) =>
        Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == remembered) ?? ForWindow(desktop);

    internal static void Place(Window window, Screen screen, bool fullscreen)
    {
        var bounds = fullscreen ? screen.Bounds : screen.WorkingArea;
        int width = fullscreen ? bounds.Width : bounds.Width * 3 / 4;
        int height = fullscreen ? bounds.Height : bounds.Height * 3 / 4;
        SetWindowPos(new WindowInteropHelper(window).EnsureHandle(), IntPtr.Zero,
            bounds.Left + (bounds.Width - width) / 2, bounds.Top + (bounds.Height - height) / 2,
            width, height, NoZOrderOrActivation);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
