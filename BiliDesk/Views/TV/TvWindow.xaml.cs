using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BiliDesk.Helpers;
using BiliDesk.Models;
using BiliDesk.ViewModels;
using BiliDesk.Views.Windows;
using Screen = System.Windows.Forms.Screen;

namespace BiliDesk.Views.TV;

internal partial class TvWindow : Window
{
    private enum Section { Recommend, Popular, History, Favorites, Search, Settings }

    // These upstream view models subscribe to application-wide events.
    private static readonly HomeViewModel _home = new();
    private static readonly SearchViewModel _search = new();
    private readonly HistoryViewModel _history = new();
    private readonly FavoritesViewModel _favorites = new();
    private readonly Dictionary<Section, Button> _navigation = new();
    private readonly List<Button> _cardButtons = new();
    private readonly TvCursor _cursor;
    private Section _section;
    private int _columns = 4;
    private bool _loading;
    private bool _favoritesLoaded;
    private bool _historyLoaded;
    private bool _closed;
    private Screen _screen;
    internal bool ActivePlayer { get; private set; }

    private sealed record Card(VideoItem Video, double CoverHeight);
    private sealed record Bookmark(string Id, int Index, double Offset);

    internal TvWindow(Screen screen)
    {
        _screen = screen;
        InitializeComponent();
        SearchBox.Text = _search.Keyword;
        foreach (var (section, label) in new[]
        {
            (Section.Recommend, "推荐"), (Section.Popular, "热门"), (Section.History, "历史"),
            (Section.Favorites, "收藏"), (Section.Search, "搜索"), (Section.Settings, "设置")
        })
        {
            Button button = AddButton(Navigation, label, () => OpenSectionAsync(section));
            _navigation.Add(section, button);
        }
        SourceInitialized += (_, _) => TvDisplay.Place(this, _screen, fullscreen: true);
        ContentScroll.SizeChanged += (_, _) => SizeCards();
        Loaded += OnLoaded;
        _cursor = new TvCursor(this);
        Closed += (_, _) => { _closed = true; _cursor.Dispose(); };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        Loaded -= OnLoaded;
        _navigation[Section.Recommend].Focus();
        await OpenSectionAsync(Section.Recommend);
    }

    internal void ShowOn(Screen screen)
    {
        _screen = screen;
        if (!IsVisible) Show();
        TvDisplay.Place(this, screen, fullscreen: true);
        Activate();
    }

    private Button AddButton(Panel panel, string text, Func<Task> click)
    {
        var button = new Button { Content = text, Style = (Style)FindResource("TvButton") };
        button.Click += async (_, _) => await click();
        panel.Children.Add(button);
        return button;
    }

    private Task OpenSectionAsync(Section section)
    {
        if (_loading) return Task.CompletedTask;
        _section = section;
        foreach (var (key, button) in _navigation)
            button.FontWeight = key == section ? FontWeights.Bold : FontWeights.Normal;
        _navigation[section].Focus();
        ContentScroll.ScrollToTop();
        Choices.Children.Clear();
        Actions.Children.Clear();
        RenderCards(Enumerable.Empty<VideoItem>());
        SearchPanel.Visibility = section == Section.Search ? Visibility.Visible : Visibility.Collapsed;
        return LoadAsync(async () =>
        {
            switch (section)
            {
                case Section.Recommend:
                    await _home.SwitchToRecommendAsync();
                    break;
                case Section.Popular:
                    await _home.SwitchToPopularAsync();
                    break;
                case Section.History when Svc.Session.HasLogin && !_historyLoaded:
                    await ReloadHistoryAsync();
                    break;
                case Section.Favorites when Svc.Session.HasLogin && !_favoritesLoaded:
                    await ReloadFavoritesAsync();
                    break;
                case Section.Search:
                    await _home.EnsureHotSearchAsync();
                    break;
            }
        });
    }

    private async Task ReloadHistoryAsync()
    {
        await _history.LoadAsync(reset: true);
        _historyLoaded = true;
    }

    private async Task ReloadFavoritesAsync()
    {
        await _favorites.LoadAsync();
        _favoritesLoaded = true;
    }

    private async Task LoadAsync(Func<Task> load, Func<UIElement?>? focusAfter = null)
    {
        if (_loading) return;
        _loading = true;
        Status.Text = "正在加载…";
        string? failure = null;
        try { await load(); }
        catch (Exception error)
        {
            App.ReportError(error);
            failure = "暂时无法加载，请重试。";
        }
        finally { _loading = false; }
        if (_closed) return;
        RenderSection();
        if (failure != null) Status.Text = failure;
        if (focusAfter != null && IsActive)
            FocusElement(focusAfter() ?? FirstContent() ?? _navigation[_section]);
    }

    private void RenderSection()
    {
        bool showSearch = _section == Section.Search;
        SearchPanel.Visibility = showSearch ? Visibility.Visible : Visibility.Collapsed;
        if (showSearch) RenderSuggestions();
        Choices.Children.Clear();
        Actions.Children.Clear();
        Status.Text = "";

        var (videos, error) = _section switch
        {
            Section.Recommend => (_home.RecommendItems, _home.Error),
            Section.Popular => (_home.PopularItems, _home.Error),
            Section.History => (_history.Entries, _history.Error),
            Section.Favorites => (_favorites.Items, _favorites.Error),
            Section.Search => (_search.Results, _search.Error),
            _ => (null, "")
        };
        RenderCards(videos is null ? Enumerable.Empty<VideoItem>() : FilterService.Visible(videos));

        if (_section == Section.Settings)
        {
            Status.Text = $"BiliDesk TV {App.TvVersion} · 上游 v{App.AppVersion}";
            if (!Svc.Session.HasLogin) AddButton(Choices, "扫码登录", LoginAsync);
            AddButton(Choices, "返回桌面模式", () => { Close(); return Task.CompletedTask; });
            return;
        }

        if ((_section is Section.History or Section.Favorites) && !Svc.Session.HasLogin)
        {
            Status.Text = "扫码登录后查看此栏目。";
            AddButton(Choices, "扫码登录", LoginAsync);
            return;
        }

        if (_section == Section.Favorites)
        {
            foreach (FavFolder folder in _favorites.Folders)
            {
                Button button = AddButton(Choices, folder.Title,
                    () => LoadAsync(() => _favorites.SelectFolderAsync(folder), () => _cardButtons.FirstOrDefault()));
                if (folder.IsCurrent) button.FontWeight = FontWeights.Bold;
            }
            if (_favorites.CanGoPrev) AddButton(Actions, "上一页", () => PageFavoritesAsync(-1));
            if (_favorites.CanGoNext) AddButton(Actions, "下一页", () => PageFavoritesAsync(1));
        }

        if (!string.IsNullOrWhiteSpace(error)) Status.Text = error;
        else if (_cardButtons.Count == 0)
        {
            Status.Text = _section == Section.Search && _search.IsIdle
                ? "选择热搜或历史词，或用键盘输入关键词。" : "暂无视频。";
        }
        if (_section == Section.Search && _search.IsIdle && !string.IsNullOrEmpty(_home.HotError))
            Status.Text = _home.HotError + "。仍可使用搜索历史或键盘输入。";

        bool canLoadMore = _section switch
        {
            Section.Recommend => !_home.RecommendExhausted && _cardButtons.Count > 0,
            Section.Popular => _home.PopularHasMore && _cardButtons.Count > 0,
            Section.History => _history.HasMore && _cardButtons.Count > 0,
            Section.Search => _search.HasMore,
            _ => false
        };
        if (canLoadMore) AddButton(Actions, "加载更多", LoadMoreAsync);
        if (_section != Section.Search || !_search.IsIdle || !string.IsNullOrEmpty(_home.HotError))
            AddButton(Actions, string.IsNullOrWhiteSpace(error) ? "刷新" : "重试", RefreshAsync);
    }

    private void RenderSuggestions()
    {
        Suggestions.Children.Clear();
        foreach (string word in _search.History.Take(5)
            .Concat(_home.HotLeft.Concat(_home.HotRight).Select(item => item.Keyword).Take(10))
            .Where(word => !string.IsNullOrWhiteSpace(word)).Distinct())
        {
            AddButton(Suggestions, word, () => SearchAsync(word));
        }
    }

    private void RenderCards(IEnumerable<VideoItem> videos)
    {
        Cards.Children.Clear();
        _cardButtons.Clear();
        foreach (VideoItem video in videos.Where(item => !item.IsLive && !string.IsNullOrEmpty(item.Bvid)))
        {
            var button = new Button
            {
                Content = new Card(video, 0), ContentTemplate = (DataTemplate)FindResource("VideoCard"),
                Style = (Style)FindResource("TvButton"), Tag = _cardButtons.Count,
                Padding = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            System.Windows.Automation.AutomationProperties.SetName(button, video.Title);
            button.Click += (_, _) => Play(video, (int)button.Tag);
            _cardButtons.Add(button);
            Cards.Children.Add(button);
        }
        SizeCards();
    }

    private void SizeCards()
    {
        double width = Math.Max(250, ContentScroll.ActualWidth);
        _columns = width >= 1600 ? 5 : Math.Clamp((int)(width / 216), 1, 4);
        double cardWidth = Math.Floor(width / _columns) - 8;
        foreach (Button button in _cardButtons)
        {
            button.Width = cardWidth;
            var card = (Card)button.Content;
            double coverHeight = Math.Max(0, (cardWidth - 14) * 9 / 16);
            button.Height = coverHeight + 94;
            if (card.CoverHeight != coverHeight) button.Content = card with { CoverHeight = coverHeight };
        }
    }

    private Task RefreshAsync() => LoadAsync(_section switch
    {
        Section.History => ReloadHistoryAsync,
        Section.Favorites => ReloadFavoritesAsync,
        Section.Search when _search.IsIdle => _home.EnsureHotSearchAsync,
        Section.Search => _search.SearchAsync,
        _ => _home.RefreshAsync
    }, () => _cardButtons.FirstOrDefault());

    private Task LoadMoreAsync()
    {
        int position = _cardButtons.Count;
        return LoadAsync(_section switch
        {
            Section.History => () => _history.LoadAsync(reset: false),
            Section.Search => _search.LoadMoreAsync,
            _ => _home.LoadMoreAsync
        }, () => _cardButtons.ElementAtOrDefault(position) ?? LastOrFirstAction());
    }

    private Task PageFavoritesAsync(int offset) => LoadAsync(
        () => _favorites.GoToPageAsync(_favorites.CurrentPage + offset), () => _cardButtons.FirstOrDefault());

    private Button? LastOrFirstAction() => Actions.Children.OfType<Button>().FirstOrDefault()
        ?? _cardButtons.LastOrDefault();

    private async void OnSearch(object sender, RoutedEventArgs args) => await SearchAsync(SearchBox.Text);

    private Task SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) { SearchBox.Focus(); return Task.CompletedTask; }
        _search.Keyword = SearchBox.Text = keyword.Trim();
        return LoadAsync(_search.SearchAsync, () => _cardButtons.FirstOrDefault() ?? LastOrFirstAction());
    }

    private Task LoginAsync()
    {
        var login = new LoginWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        login.PreviewKeyDown += OnLoginKeyDown;
        login.ShowDialog();
        if (!Svc.Session.HasLogin) { FocusElement(FirstContent() ?? _navigation[_section]); return Task.CompletedTask; }
        _historyLoaded = _favoritesLoaded = false;
        return OpenSectionAsync(_section);
    }

    private static void OnLoginKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key is Key.Escape or Key.BrowserBack ||
            (args.Key == Key.Back && Keyboard.FocusedElement is not TextBox and not PasswordBox))
        {
            args.Handled = true;
            ((Window)sender).Close();
        }
        else if (Keyboard.FocusedElement is UIElement current and not TextBox and not PasswordBox
            && Direction(args.Key) is { } direction)
        {
            args.Handled = current.MoveFocus(new TraversalRequest(direction));
        }
    }

    private void Play(VideoItem video, int index)
    {
        if (_loading || ActivePlayer) return;
        var bookmark = new Bookmark(video.Bvid, index, ContentScroll.VerticalOffset);
        ActivePlayer = Svc.Player.PlayTvVideo(video, this, () => RestorePlaybackFocus(bookmark));
        if (!ActivePlayer)
            Status.Text = "播放器启动失败。可再次选择视频重试。";
    }

    private void RestorePlaybackFocus(Bookmark bookmark)
    {
        ActivePlayer = false;
        if (_closed || App.IsExiting) return;
        Activate();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (_closed) return;
            int index = TvNavigation.RestoreIndex(_cardButtons.Select(button => ((Card)button.Content).Video.Bvid).ToArray(),
                bookmark.Id, bookmark.Index);
            ContentScroll.ScrollToVerticalOffset(bookmark.Offset);
            FocusElement(index >= 0 ? _cardButtons[index] : FirstContent() ?? _navigation[_section]);
        });
    }

    private static FocusNavigationDirection? Direction(Key key) => key switch
    {
        Key.Left => FocusNavigationDirection.Left, Key.Right => FocusNavigationDirection.Right,
        Key.Up => FocusNavigationDirection.Up, Key.Down => FocusNavigationDirection.Down, _ => null
    };

    private UIElement? FirstContent() => SearchPanel.IsVisible ? SearchBox
        : Choices.Children.OfType<Button>().FirstOrDefault() ?? _cardButtons.FirstOrDefault() ?? LastOrFirstAction();

    private static void FocusElement(UIElement element)
    {
        element.Focus();
        if (element is FrameworkElement frameworkElement) frameworkElement.BringIntoView();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && args.IsRepeat) { args.Handled = true; return; }
        if (Keyboard.FocusedElement is not UIElement current)
        {
            FocusElement(_navigation[_section]);
            return;
        }
        if (args.Key == Key.Enter && ReferenceEquals(current, SearchBox))
        {
            args.Handled = true;
            _ = SearchAsync(SearchBox.Text);
            return;
        }
        if (args.Key == Key.Enter && current is Button { IsEnabled: true } button)
        {
            args.Handled = true;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
            return;
        }
        if (args.Key is Key.Escape or Key.BrowserBack || (args.Key == Key.Back && current != SearchBox))
        {
            args.Handled = true;
            if (current != _navigation[_section]) FocusElement(_navigation[_section]);
            else if (_section != Section.Recommend) _ = OpenSectionAsync(Section.Recommend);
            return;
        }
        if (Direction(args.Key) is not { } direction || Keyboard.Modifiers != ModifierKeys.None) return;
        if (current == SearchBox && args.Key is Key.Left or Key.Right) return;
        if (current is Button { Tag: int index })
        {
            int target = args.Key switch
            {
                Key.Left when index % _columns > 0 => index - 1,
                Key.Right when index % _columns < _columns - 1 && index + 1 < _cardButtons.Count => index + 1,
                Key.Up when index >= _columns => index - _columns,
                Key.Down when index + _columns < _cardButtons.Count => index + _columns,
                Key.Down when (index / _columns + 1) * _columns < _cardButtons.Count => _cardButtons.Count - 1,
                _ => -1
            };
            if (target >= 0) FocusElement(_cardButtons[target]);
            else if (args.Key == Key.Up)
                FocusElement(Choices.Children.OfType<Button>().LastOrDefault()
                    ?? (UIElement?)Suggestions.Children.OfType<Button>().LastOrDefault()
                    ?? (SearchPanel.IsVisible ? SearchBox : _navigation[_section]));
            else if (args.Key == Key.Down && Actions.Children.OfType<Button>().FirstOrDefault() is { } action)
                FocusElement(action);
            args.Handled = true;
            return;
        }
        if (args.Key == Key.Down && current is Button nav && _navigation.Values.Contains(nav))
        {
            if (FirstContent() is { } first) FocusElement(first);
            args.Handled = true;
            return;
        }
        if (!current.MoveFocus(new TraversalRequest(direction)) && args.Key == Key.Up)
            FocusElement(_navigation[_section]);
        args.Handled = true;
    }
}
