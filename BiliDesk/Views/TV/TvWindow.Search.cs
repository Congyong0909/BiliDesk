using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiliDesk.Models;

namespace BiliDesk.Views.TV;

internal partial class TvWindow
{
    private SearchUserItem? _creator;
    private Bookmark? _searchBookmark;
    private IReadOnlyList<VideoItem> _creatorVideos = Array.Empty<VideoItem>();
    private int _creatorPage;
    private bool _creatorHasMore;
    private bool _creatorRetryNextPage;
    private string _creatorError = "";

    private void RenderSearchKinds()
    {
        SearchKinds.Children.Clear();
        foreach (var (kind, label) in new[] { (ContentKind.Video, "视频"), (ContentKind.User, "UP 主") })
        {
            var button = AddButton(SearchKinds, label, () => _search.Kind == kind ? Task.CompletedTask
                : LoadAsync(() => _search.ChangeKindAsync(kind), () => _cardButtons.FirstOrDefault()
                    ?? (UIElement?)SearchKinds.Children.OfType<Button>().FirstOrDefault(item => (ContentKind)item.Tag == kind)));
            button.Tag = kind;
            if (_search.Kind == kind) button.FontWeight = FontWeights.Bold;
        }
    }

    private Task OpenCreatorAsync(SearchUserItem user, int index)
    {
        if (_loading || ActivePlayer) return Task.CompletedTask;
        _searchBookmark = new Bookmark(((Card)_cardButtons[index].Content).Id, index, ContentScroll.VerticalOffset);
        _creator = user;
        _creatorVideos = Array.Empty<VideoItem>();
        _creatorHasMore = false;
        _creatorPage = 0;
        ContentScroll.ScrollToTop();
        SearchPanel.Visibility = Visibility.Collapsed;
        RenderCards(Enumerable.Empty<Card>());
        return LoadAsync(() => LoadCreatorAsync(reset: true), () => _cardButtons.FirstOrDefault());
    }

    private async Task LoadCreatorAsync(bool reset)
    {
        if (_creator == null) return;
        const int pageSize = 30;
        int page = reset ? 1 : _creatorPage + 1;
        var (ok, error, videos, total) = await Svc.Api.GetSpaceVideosAsync(_creator.Mid, page, pageSize);
        _creatorError = ok ? "" : error ?? "投稿加载失败。";
        _creatorRetryNextPage = !ok && !reset;
        if (!ok || videos == null) return;

        var previous = reset ? Array.Empty<VideoItem>() : _creatorVideos;
        _creatorVideos = previous.Concat(videos.Where(item => !string.IsNullOrEmpty(item.Bvid)))
            .OrderByDescending(item => item.Pubdate)
            .DistinctBy(item => item.Bvid, StringComparer.Ordinal)
            .ToArray();
        _creatorPage = page;
        _creatorHasMore = total > 0 ? page * pageSize < total
            : videos.Count == pageSize && _creatorVideos.Count > previous.Count;
    }

    private void CloseCreator()
    {
        if (_loading || _creator == null) return;
        _creator = null;
        RenderSection();
        if (_searchBookmark is { } bookmark) RestoreBookmark(bookmark);
        _searchBookmark = null;
    }
}
