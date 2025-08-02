using UnityEngine;

namespace BetterOutfitStand;

public static class RectUtils
{
    public static Rect SplitHoriPartPixels(this Rect rect, float splitAt, out Rect bottom)
    {
        Rect top = new(rect.x, rect.y, rect.width, splitAt);
        bottom = new Rect(rect.x, rect.y + splitAt, rect.width, rect.height - splitAt);
        return top;
    }

    public static Rect SplitVertPartPixels(this Rect rect, float splitAt, out Rect right)
    {
        Rect left = new(rect.x, rect.y, splitAt, rect.height);
        right = new Rect(rect.x + splitAt, rect.y, rect.width - splitAt, rect.height);
        return left;
    }
}
