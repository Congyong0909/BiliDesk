using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using BiliDesk.Models;
using Microsoft.Win32;

namespace BiliDesk.Views.TV;

internal sealed class TvTheme : IDisposable
{
    internal static readonly (string Key, uint Light, uint Dark)[] Colors =
    {
        ("TvSurface", 0xF4F7FB, 0x10151E),
        ("TvPanel", 0xF4F7FB, 0x182230),
        ("TvButtonBackground", 0xE5ECF5, 0x253246),
        ("TvCoverBackground", 0xD6E0EC, 0x141D2B),
        ("TvInputBackground", 0xFFFFFF, 0x202C3D),
        ("TvText", 0x121E2F, 0xFAFCFF),
        ("TvTextSecondary", 0x354760, 0xCED9E8),
        ("TvBorder", 0x627690, 0x8C9DB4),
        ("TvFocus", 0x075D85, 0x8EEAFF),
        ("TvFocusedBackground", 0xD2E6F6, 0x254459),
        ("TvProgressTrack", 0xB7C8DA, 0x39495D),
    };

    private readonly ResourceDictionary _resources = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool? _dark;
    private bool _disposed;

    internal TvTheme()
    {
        Refresh();
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    internal void Attach(FrameworkElement element) => element.Resources.MergedDictionaries.Add(_resources);

    internal static bool IsDark(TvThemeMode mode, TimeSpan localTime) => mode switch
    {
        TvThemeMode.Light => false,
        TvThemeMode.Dark => true,
        _ => localTime.Hours is < 7 or >= 19
    };

    internal void Refresh()
    {
        if (_disposed) return;
        bool dark = IsDark(Svc.Settings.TvTheme, DateTime.Now.TimeOfDay);
        if (_dark == dark) return;
        _dark = dark;
        foreach (var (key, light, night) in Colors)
        {
            uint rgb = dark ? night : light;
            var brush = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            brush.Freeze();
            _resources[key] = brush;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode == PowerModes.Resume) _timer.Dispatcher.InvokeAsync(Refresh);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
