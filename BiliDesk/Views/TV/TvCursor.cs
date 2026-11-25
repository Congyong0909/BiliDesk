using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BiliDesk.Views.TV;

internal sealed class TvCursor : IDisposable
{
    private readonly FrameworkElement _surface;
    private readonly DispatcherTimer _timer;
    private Point? _lastPosition;

    internal TvCursor(FrameworkElement surface)
    {
        _surface = surface;
        _timer = new DispatcherTimer(DispatcherPriority.Background, surface.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _timer.Tick += Hide;
        _surface.PreviewMouseMove += OnMouseMove;
        _timer.Start();
    }

    private void Hide(object? sender, EventArgs args)
    {
        _timer.Stop();
        _surface.ForceCursor = true;
        _surface.Cursor = Cursors.None;
        Mouse.UpdateCursor();
    }

    private void OnMouseMove(object sender, MouseEventArgs args)
    {
        Point position = args.GetPosition(_surface);
        if (_lastPosition == position) return;
        _lastPosition = position;
        Restore();
        _timer.Start();
    }

    private void Restore()
    {
        _timer.Stop();
        _surface.ClearValue(FrameworkElement.CursorProperty);
        _surface.ClearValue(FrameworkElement.ForceCursorProperty);
        Mouse.UpdateCursor();
    }

    public void Dispose()
    {
        _surface.PreviewMouseMove -= OnMouseMove;
        _timer.Tick -= Hide;
        Restore();
    }
}
