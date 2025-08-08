using System;
using Verse;

namespace Blueprint2;

public enum PlaceMode
{
    FloorOnly,
    Bridges,
    Buildings
}

public static class PlaceModeExtensions
{
    public static string GetLabel(this PlaceMode mode)
    {
        return mode switch
        {
            PlaceMode.FloorOnly => "Blueprint2.Floors".Translate(),
            PlaceMode.Bridges => "Blueprint2.Bridges".Translate(),
            PlaceMode.Buildings => "Blueprint2.Buildings".Translate(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
}
