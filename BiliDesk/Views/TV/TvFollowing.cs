using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BiliDesk.Models;

namespace BiliDesk.Views.TV;

internal sealed class TvFollowing
{
    private string? _offset;
    private string? _account;

    internal IReadOnlyList<VideoItem> Videos { get; private set; } = Array.Empty<VideoItem>();
    internal string Error { get; private set; } = "";
    internal bool HasMore { get; private set; }
    internal bool IsLoaded { get; private set; }
    internal bool RetryNextPage { get; private set; }

    internal void SetAccount(string? account)
    {
        if (_account == account) return;
        _account = account;
        _offset = null;
        Videos = Array.Empty<VideoItem>();
        Error = "";
        HasMore = IsLoaded = RetryNextPage = false;
    }

    internal async Task LoadAsync(bool reset)
    {
        if (_account == null || (!reset && !HasMore)) return;
        var account = _account;
        var offset = reset ? null : _offset;
        var videos = reset ? Array.Empty<VideoItem>() : Videos;
        Error = "";
        RetryNextPage = false;

        // The feed includes non-video posts. Look ahead a few pages, then offer manual paging.
        for (int page = 0; page < 3; page++)
        {
            var (ok, error, items, next, hasMore) = await Svc.Api.GetDynamicFeedAsync(offset);
            if (account != _account) return;
            if (!ok || items == null)
            {
                Error = error ?? "关注视频加载失败。";
                RetryNextPage = !reset;
                return;
            }

            long.TryParse(account, out long ownMid);
            var merged = MergeVideos(videos, items, ownMid);
            HasMore = hasMore && !string.IsNullOrEmpty(next) && next != offset;
            _offset = next;
            IsLoaded = true;
            Videos = merged;
            if (merged.Count > videos.Count || !HasMore) return;
            videos = merged;
            offset = next;
        }
    }

    internal static IReadOnlyList<VideoItem> MergeVideos(IEnumerable<VideoItem> existing, IEnumerable<DynamicItem> page,
        long ownMid = 0) =>
        existing.Concat(page.Where(item => item.HasPlayableVideo && !item.IsForward && item.AuthorFollowing != false &&
                (ownMid <= 0 || item.AuthorMid != ownMid))
                .Select(item => item.AsVideoItem()!))
            .OrderByDescending(video => video.Pubdate)
            .ThenBy(video => video.Bvid, StringComparer.Ordinal)
            .DistinctBy(video => video.Bvid, StringComparer.Ordinal)
            .ToArray();
}
