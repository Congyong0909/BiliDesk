using System;
using System.Collections.Generic;

namespace BiliDesk.Views.TV;

internal enum TvPlayerLayer { Hidden, Controls, Menu }
internal enum TvKey { Left, Right, Up, Down, Confirm, Back }
internal enum TvPlayerAction { SeekBack, SeekForward, VolumeUp, VolumeDown, ShowControls, Move, Invoke, CloseMenu, HideControls, Exit }

internal static class TvNavigation
{
    internal static TvPlayerAction PlayerAction(TvPlayerLayer layer, TvKey key) => (layer, key) switch
    {
        (TvPlayerLayer.Hidden, TvKey.Left) => TvPlayerAction.SeekBack,
        (TvPlayerLayer.Hidden, TvKey.Right) => TvPlayerAction.SeekForward,
        (TvPlayerLayer.Hidden, TvKey.Up) => TvPlayerAction.VolumeUp,
        (TvPlayerLayer.Hidden, TvKey.Down) => TvPlayerAction.VolumeDown,
        (TvPlayerLayer.Hidden, TvKey.Confirm) => TvPlayerAction.ShowControls,
        (TvPlayerLayer.Hidden, TvKey.Back) => TvPlayerAction.Exit,
        (TvPlayerLayer.Controls, TvKey.Back) => TvPlayerAction.HideControls,
        (TvPlayerLayer.Menu, TvKey.Back) => TvPlayerAction.CloseMenu,
        (_, TvKey.Confirm) => TvPlayerAction.Invoke,
        _ => TvPlayerAction.Move
    };

    internal static int RestoreIndex(IReadOnlyList<string> ids, string id, int position)
    {
        if (ids.Count == 0) return -1;
        position = Math.Clamp(position, 0, ids.Count - 1);
        if (ids[position] == id) return position;
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == id) return i;
        return position;
    }
}
