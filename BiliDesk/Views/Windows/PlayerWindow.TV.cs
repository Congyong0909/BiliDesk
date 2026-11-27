using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BiliDesk.Helpers;
using BiliDesk.Views.TV;

namespace BiliDesk.Views;

public partial class PlayerWindow
{
    private TvPlaybackControls? _tvPlayback;
    private TvCursor? _tvCursor;
    private TvTheme? _tvTheme;
    private bool IsTvPresentation => _tvPlayback != null;
    private bool TvDolbyEnabled => IsTvPresentation && Svc.Settings.TvDolbyVision;
    private DispatcherTimer? _tvDolbyStartup;
    private TvDolbyFallback? _tvDolbyFallback;

    private sealed record TvDolbyFallback(int Epoch, int Quality, long Position, string Url,
        string? AudioUrl, string? ListenAudioUrl, string[] Backups);

    internal void BeginTvPresentation(TvWindow owner)
    {
        if (IsTvPresentation) return;
        if (_isFullscreen) ToggleFullscreen();
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        TvDisplay.Place(this, TvDisplay.ForWindow(owner), fullscreen: false);
        ToggleFullscreen(persistPlacement: false);

        _controlsHideTimer?.Stop();
        ShowControls();
        ControlBar.Visibility = Visibility.Collapsed;
        _tvPlayback = new TvPlaybackControls(TimeText, ProgressSlider) { VerticalAlignment = VerticalAlignment.Bottom };
        _tvTheme = owner.Theme;
        _tvTheme.Attach(_tvPlayback);
        _tvTheme.Refresh();
        Activated += OnTvActivated;
        System.Windows.Controls.Panel.SetZIndex(_tvPlayback, 10);
        VideoAreaRoot.Children.Add(_tvPlayback);
        _tvCursor = new TvCursor(VideoAreaRoot);
        HookOverlayKeyboard();
        Activate();
        Focus();
    }

    internal void EndTvPresentation(bool restoreDesktop)
    {
        if (_tvPlayback == null) return;
        ClearTvDolbyFallback();
        Activated -= OnTvActivated;
        _tvTheme = null;
        _tvCursor?.Dispose();
        _tvCursor = null;
        VideoAreaRoot.Children.Remove(_tvPlayback);
        _tvPlayback = null;
        if (!restoreDesktop) return;
        ControlBar.Visibility = Visibility.Visible;
        if (_isFullscreen) ToggleFullscreen();
    }

    private void OnTvActivated(object? sender, System.EventArgs args) => _tvTheme?.Refresh();

    private void ArmTvDolbyFallback(long position)
    {
        ClearTvDolbyFallback();
        if (string.IsNullOrEmpty(_lastPlayUrl)) return;
        _tvDolbyFallback = new TvDolbyFallback(_mediaEpoch + 1, _currentQn, position, _lastPlayUrl,
            _lastAudioUrl, _listenAudioUrl, _backups.ToArray());
        _tvDolbyStartup = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(15) };
        _tvDolbyStartup.Tick += (_, _) =>
        {
            if (!TryRestoreTvDolby(_mediaEpoch)) ClearTvDolbyFallback();
        };
        _tvDolbyStartup.Start();
    }

    private void ClearTvDolbyFallback()
    {
        _tvDolbyStartup?.Stop();
        _tvDolbyStartup = null;
        _tvDolbyFallback = null;
    }

    private bool TryRestoreTvDolby(int epoch)
    {
        if (_closing || _tvDolbyFallback is not { } fallback || fallback.Epoch != epoch) return false;
        ClearTvDolbyFallback();
        CancelRemuxPrefetch();
        _lastPlayUrl = fallback.Url;
        _lastAudioUrl = fallback.AudioUrl;
        _listenAudioUrl = fallback.ListenAudioUrl;
        RememberPlayUrlFreshness(fallback.Url, fallback.Backups);
        _resumeAfterSwitchMs = fallback.Position;
        _resumeByStartTime = false;
        FillQualityMenu(fallback.Quality);
        _tvPlayback?.UpdateLabel(1, TvQualityLabel);
        ShowLoading("正在恢复上一个画质…");
        OnToast("杜比视界播放失败，正在恢复上一个画质");
        try
        {
            _mp.Stop();
            if (!PlayMedia(BuildMedia(fallback.Url, fallback.AudioUrl)))
                ShowFail("恢复播放失败，请重试。");
        }
        catch (System.Exception error)
        {
            App.ReportError(error);
            ShowFail("恢复播放失败，请重试。");
        }
        UpdatePlayButton();
        return true;
    }

    private bool HandleTvInput(KeyEventArgs args)
    {
        if (_tvPlayback == null) return false;
        TvKey? key = args.Key switch
        {
            Key.Left => TvKey.Left, Key.Right => TvKey.Right, Key.Up => TvKey.Up, Key.Down => TvKey.Down,
            Key.Enter => TvKey.Confirm, Key.Escape or Key.Back or Key.BrowserBack => TvKey.Back, _ => null
        };
        if (key == null) return args.Key == Key.F;
        if (args.IsRepeat && key is TvKey.Confirm or TvKey.Back) return true;
        if (key == TvKey.Confirm && FailOverlay.IsVisible && _tvPlayback.Layer == TvPlayerLayer.Hidden)
        {
            OnRetryClick(this, new RoutedEventArgs());
            return true;
        }

        switch (TvNavigation.PlayerAction(_tvPlayback.Layer, key.Value))
        {
            case TvPlayerAction.SeekBack: SeekRelative(-5); break;
            case TvPlayerAction.SeekForward: SeekRelative(5); break;
            case TvPlayerAction.VolumeUp: SetVolume(_volumeCache + VolumeStep); break;
            case TvPlayerAction.VolumeDown: SetVolume(_volumeCache - VolumeStep); break;
            case TvPlayerAction.ShowControls: ShowTvControls(); break;
            case TvPlayerAction.Move: _tvPlayback.Move(key.Value); break;
            case TvPlayerAction.Invoke: _tvPlayback.Invoke(); break;
            case TvPlayerAction.CloseMenu: _tvPlayback.CloseMenu(); break;
            case TvPlayerAction.HideControls: _tvPlayback.Hide(); FocusTvHost(); break;
            case TvPlayerAction.Exit: Close(); break;
        }
        return true;
    }

    private void FocusTvHost()
    {
        Window host = _overlayKeyboardWindow?.IsActive == true ? _overlayKeyboardWindow : this;
        host.Focus();
    }

    private void ShowTvControls()
    {
        _tvPlayback?.Show(
            new("播放／暂停", () => OnPlayPauseClick(this, new RoutedEventArgs())),
            new(TvQualityLabel, ShowTvQualities),
            new(TvDanmakuLabel, () => { ToggleDanmaku(); _tvPlayback?.UpdateLabel(2, TvDanmakuLabel); }),
            new("分 P", ShowTvParts),
            new("选集", ShowTvEpisodes));
    }

    private string TvQualityLabel => $"画质 · {QualityText.Text}";
    private string TvDanmakuLabel => _danmakuOn ? "弹幕 · 开" : "弹幕 · 关";

    private void ShowTvQualities()
    {
        var items = _qualities.Where(item => QualityMenuPolicy.IsMenuTier(item.qn, TvDolbyEnabled)).ToArray();
        if (items.Length == 0) { ShowTvMessage("当前暂无可选画质"); return; }
        _tvPlayback?.OpenMenu(items.Select(item => new TvPlaybackControls.Entry(item.label, async () =>
        {
            _tvPlayback?.CloseMenu();
            if (item.qn != _currentQn) await SwitchQualityAsync(item.qn);
            _tvPlayback?.UpdateLabel(1, TvQualityLabel);
        }, item.qn == _currentQn)));
    }

    private void ShowTvParts()
    {
        if (_pages.Count <= 1) { ShowTvMessage("当前视频暂无分 P"); return; }
        _tvPlayback?.OpenMenu(_pages.Select(part => new TvPlaybackControls.Entry($"P{part.Page}  {part.Part}", async () =>
        {
            _tvPlayback?.CloseMenu();
            if (part.Cid != _currentCid) await SwitchToPageAsync(part);
            _tvPlayback?.UpdateLabel(1, TvQualityLabel);
        }, part.Cid == _currentCid)));
    }

    private void ShowTvEpisodes()
    {
        if (_season?.Episodes.Count is not > 0) { ShowTvMessage("当前视频暂无选集"); return; }
        _tvPlayback?.OpenMenu(_season.Episodes.Where(episode => !string.IsNullOrEmpty(episode.Bvid))
            .Select(episode => new TvPlaybackControls.Entry(episode.Title, async () =>
            {
                _tvPlayback?.CloseMenu();
                if (episode != _currentSeasonEpisode) await SwitchToSeasonEpisodeAsync(episode);
                _tvPlayback?.UpdateLabel(1, TvQualityLabel);
            }, episode == _currentSeasonEpisode)));
    }

    private void ShowTvMessage(string text) => _tvPlayback?.OpenMenu(new[]
    {
        new TvPlaybackControls.Entry(text + " · 返回", () => _tvPlayback?.CloseMenu())
    });
}
