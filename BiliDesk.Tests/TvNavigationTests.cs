using BiliDesk.Views.TV;

namespace BiliDesk.Tests;

public class TvNavigationTests
{
    [Fact]
    public void BackClosesMenuThenControlsThenPlayback()
    {
        Assert.Equal(TvPlayerAction.CloseMenu, TvNavigation.PlayerAction(TvPlayerLayer.Menu, TvKey.Back));
        Assert.Equal(TvPlayerAction.HideControls, TvNavigation.PlayerAction(TvPlayerLayer.Controls, TvKey.Back));
        Assert.Equal(TvPlayerAction.Exit, TvNavigation.PlayerAction(TvPlayerLayer.Hidden, TvKey.Back));
    }

    [Fact]
    public void DirectionalKeysControlPlaybackOnlyWhileControlsAreHidden()
    {
        Assert.Equal(TvPlayerAction.SeekBack, TvNavigation.PlayerAction(TvPlayerLayer.Hidden, TvKey.Left));
        Assert.Equal(TvPlayerAction.SeekForward, TvNavigation.PlayerAction(TvPlayerLayer.Hidden, TvKey.Right));
        Assert.Equal(TvPlayerAction.VolumeUp, TvNavigation.PlayerAction(TvPlayerLayer.Hidden, TvKey.Up));
        Assert.Equal(TvPlayerAction.VolumeDown, TvNavigation.PlayerAction(TvPlayerLayer.Hidden, TvKey.Down));
        foreach (TvKey key in new[] { TvKey.Left, TvKey.Right, TvKey.Up, TvKey.Down })
        {
            Assert.Equal(TvPlayerAction.Move, TvNavigation.PlayerAction(TvPlayerLayer.Controls, key));
            Assert.Equal(TvPlayerAction.Move, TvNavigation.PlayerAction(TvPlayerLayer.Menu, key));
        }
    }

    [Fact]
    public void ConfirmOpensControlsOrInvokesFocusedItem()
    {
        Assert.Equal(TvPlayerAction.ShowControls, TvNavigation.PlayerAction(TvPlayerLayer.Hidden, TvKey.Confirm));
        Assert.Equal(TvPlayerAction.Invoke, TvNavigation.PlayerAction(TvPlayerLayer.Controls, TvKey.Confirm));
        Assert.Equal(TvPlayerAction.Invoke, TvNavigation.PlayerAction(TvPlayerLayer.Menu, TvKey.Confirm));
    }

    [Fact]
    public void FocusReturnsToTheSameVideoEvenIfItMoved()
    {
        Assert.Equal(2, TvNavigation.RestoreIndex(new[] { "A", "C", "B" }, "B", 1));
        Assert.Equal(2, TvNavigation.RestoreIndex(new[] { "B", "C", "B" }, "B", 2));
    }

    [Fact]
    public void FocusFallsBackToNearestCardAndHandlesEmptyList()
    {
        Assert.Equal(1, TvNavigation.RestoreIndex(new[] { "A", "C", "D" }, "B", 1));
        Assert.Equal(2, TvNavigation.RestoreIndex(new[] { "A", "C", "D" }, "B", 4));
        Assert.Equal(-1, TvNavigation.RestoreIndex(Array.Empty<string>(), "B", 4));
    }
}
