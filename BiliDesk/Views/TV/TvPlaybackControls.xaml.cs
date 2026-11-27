using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace BiliDesk.Views.TV;

internal partial class TvPlaybackControls : UserControl
{
    internal sealed record Entry(string Label, Action Run, bool Current = false);

    private Button? _commandFocus;
    private Button? _menuFocus;

    internal TvPlaybackControls(TextBlock time, Slider progress)
    {
        InitializeComponent();
        Clock.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = time, Mode = BindingMode.OneWay });
        Progress.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(Slider.Value)) { Source = progress, Mode = BindingMode.OneWay });
        Visibility = Visibility.Collapsed;
    }

    internal TvPlayerLayer Layer => Visibility != Visibility.Visible ? TvPlayerLayer.Hidden
        : Menu.Visibility == Visibility.Visible ? TvPlayerLayer.Menu : TvPlayerLayer.Controls;

    internal void Show(params Entry[] entries)
    {
        Visibility = Visibility.Visible;
        _commandFocus = Fill(Commands, entries, out _);
        Highlight(_commandFocus);
    }

    internal void Hide()
    {
        CloseMenu();
        Visibility = Visibility.Collapsed;
    }

    internal void OpenMenu(IEnumerable<Entry> entries)
    {
        Menu.Visibility = Visibility.Visible;
        Button? first = Fill(Options, entries, out Button? current);
        _menuFocus = current ?? first;
        Highlight(_menuFocus);
    }

    internal void CloseMenu()
    {
        if (Menu.Visibility == Visibility.Collapsed) return;
        Options.Children.Clear();
        _menuFocus = null;
        Menu.Visibility = Visibility.Collapsed;
        Highlight(_commandFocus);
    }

    internal void Move(TvKey key)
    {
        bool menu = Layer == TvPlayerLayer.Menu;
        Panel panel = menu ? Options : Commands;
        var children = panel.Children;
        Button? selected = menu ? _menuFocus : _commandFocus;
        int index = selected == null ? -1 : children.IndexOf(selected);
        if (index < 0) return;
        int step = key is TvKey.Left or TvKey.Up ? -1 : 1;
        int next = Math.Clamp(index + step, 0, children.Count - 1);
        if (next == index) return;
        selected!.ClearValue(Control.BorderBrushProperty);
        var target = (Button)children[next];
        if (menu) _menuFocus = target;
        else _commandFocus = target;
        Highlight(target);
    }

    internal void Invoke()
    {
        Button? selection = Layer == TvPlayerLayer.Menu ? _menuFocus : _commandFocus;
        if (selection?.Tag is Entry entry) entry.Run();
    }

    internal void UpdateLabel(int index, string label)
    {
        if (Commands.Children.Count > index && Commands.Children[index] is Button button)
            button.Content = label;
    }

    private Button? Fill(Panel panel, IEnumerable<Entry> entries, out Button? current)
    {
        panel.Children.Clear();
        current = null;
        foreach (Entry entry in entries)
        {
            var item = new Button
            {
                Tag = entry, Content = entry.Label, Style = (Style)FindResource("TvButton"),
                FontWeight = entry.Current ? FontWeights.Bold : FontWeights.Normal
            };
            item.Click += (_, _) =>
            {
                if (panel == Options) { _menuFocus = item; }
                else if (!ReferenceEquals(item, _commandFocus))
                {
                    _commandFocus?.ClearValue(Control.BorderBrushProperty);
                    _commandFocus = item;
                    Highlight(item);
                }
                entry.Run();
            };
            panel.Children.Add(item);
            if (entry.Current) current = item;
        }
        return panel.Children.OfType<Button>().FirstOrDefault();
    }

    private void Highlight(Button? button)
    {
        if (button == null) return;
        button.SetResourceReference(Control.BorderBrushProperty, "TvFocus");
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!button.IsVisible || button != (Layer == TvPlayerLayer.Menu ? _menuFocus : _commandFocus)) return;
            button.Focus();
            button.BringIntoView();
        });
    }
}
