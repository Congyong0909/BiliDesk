using BiliDesk.Helpers;
using BiliDesk.Models;
using BiliDesk.Views.TV;
using Xunit;

namespace BiliDesk.Tests;

public class TvUpdateTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(18, 59, false)]
    [InlineData(19, 0, true)]
    public void AutomaticThemeUsesLocalTime(int hour, int minute, bool dark) =>
        Assert.Equal(dark, TvTheme.IsDark(TvThemeMode.Automatic, new TimeSpan(hour, minute, 0)));

    [Fact]
    public void ManualThemesOverrideTheClockAndKeepTextReadable()
    {
        Assert.False(TvTheme.IsDark(TvThemeMode.Light, TimeSpan.Zero));
        Assert.True(TvTheme.IsDark(TvThemeMode.Dark, TimeSpan.FromHours(10)));
        var colors = TvTheme.Colors.ToDictionary(color => color.Key);
        foreach (string text in new[] { "TvText", "TvTextSecondary" })
        foreach (string surface in new[] { "TvSurface", "TvPanel", "TvButtonBackground", "TvFocusedBackground", "TvInputBackground" })
        {
            Assert.True(Contrast(colors[text].Light, colors[surface].Light) >= 4.5, $"Light {text} on {surface}");
            Assert.True(Contrast(colors[text].Dark, colors[surface].Dark) >= 4.5, $"Dark {text} on {surface}");
        }
    }

    [Fact]
    public void FollowingMergesDirectUploadsInPublicationOrderAcrossPages()
    {
        var first = TvFollowing.MergeVideos(Array.Empty<VideoItem>(), new[]
        {
            Upload("BVmiddle", 370, 300), Upload("BVold", 200), Upload("BVnewest", 500)
        });
        var second = TvFollowing.MergeVideos(first, new[]
        {
            Upload("BVbetween", 350), Upload("BVmiddle", 900, 300),
            new DynamicItem { Kind = DynamicKind.Forward, Orig = Upload("BVshared", 800), Pubdate = 850 },
            new DynamicItem { Kind = DynamicKind.Video, Bvid = "BVrecommendation", AuthorFollowing = false, Pubdate = 650 },
            new DynamicItem { Kind = DynamicKind.Video, Bvid = "BVself", AuthorMid = 7, Pubdate = 750 },
            new DynamicItem { Kind = DynamicKind.Text, Pubdate = 600 }
        }, ownMid: 7);

        Assert.Equal(new[] { "BVnewest", "BVbetween", "BVmiddle", "BVold" }, second.Select(video => video.Bvid));
        Assert.Equal(300, second[2].Pubdate);
    }

    [Fact]
    public void DolbyVisionIsOptInAndRequiresAnExactExplicitSelection()
    {
        Assert.Equal("16", QualityMenuPolicy.DashFlags(false, false));
        Assert.Equal("4048", QualityMenuPolicy.DashFlags(false, true));
        var incoming = new[] { (127, "8K"), (126, "杜比视界"), (125, "HDR"), (120, "4K") };
        var hidden = QualityMenuPolicy.Merge(Array.Empty<(int, string)>(), incoming);
        Assert.Equal(new[] { 120 }, hidden.Select(item => item.qn));
        var visible = QualityMenuPolicy.Merge(hidden, incoming, allowDolbyVision: true);
        Assert.Equal(new[] { (126, QualityMenuPolicy.DolbyVisionLabel), (120, "4K") }, visible);
        Assert.Equal(visible, QualityMenuPolicy.Merge(visible, Array.Empty<(int, string)>(), allowDolbyVision: true));
        Assert.False(QualityMenuPolicy.CanSelectTvDashTier(126, 120, true));
        Assert.False(QualityMenuPolicy.CanSelectTvDashTier(126, 126, false));
        Assert.False(QualityMenuPolicy.CanSelectTvDashTier(120, 126, true));
        Assert.True(QualityMenuPolicy.CanSelectTvDashTier(126, 126, true));
    }

    private static DynamicItem Upload(string bvid, long postTime, long videoTime = 0) => new()
    {
        Kind = DynamicKind.Video, Bvid = bvid, Pubdate = postTime, VideoPubdate = videoTime, AuthorFollowing = true
    };

    private static double Contrast(uint first, uint second)
    {
        double one = Luminance(first), two = Luminance(second);
        return (Math.Max(one, two) + 0.05) / (Math.Min(one, two) + 0.05);
    }

    private static double Luminance(uint rgb)
    {
        static double Linear(uint value)
        {
            double srgb = (value & 0xff) / 255.0;
            return srgb <= 0.04045 ? srgb / 12.92 : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }
        return Linear(rgb >> 16) * 0.2126 + Linear(rgb >> 8) * 0.7152 + Linear(rgb) * 0.0722;
    }
}
