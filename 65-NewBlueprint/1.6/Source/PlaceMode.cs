using System;
using Verse;

namespace Blueprint2;

public enum PlaceMode
{
    TerrainOnly,
    BuildingsOnly
}

public static class PlaceModeExtensions
{
    public static string GetLabel(this PlaceMode mode)
    {
        return mode switch
        {
            PlaceMode.TerrainOnly => "Blueprint2.Terrain".Translate(),
            PlaceMode.BuildingsOnly => "Blueprint2.Buildings".Translate(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
}
