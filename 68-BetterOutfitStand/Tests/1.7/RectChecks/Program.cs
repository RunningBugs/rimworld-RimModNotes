using BetterOutfitStand;
using UnityEngine;
foreach (float split in new[] { 0f, 25f, 80f })
{
    Rect source = new(7, 13, 80, 80);
    Rect top = source.SplitHoriPartPixels(split, out Rect bottom);
    Rect left = source.SplitVertPartPixels(split, out Rect right);
    if (top.x != source.x || top.y != source.y || top.width != source.width || top.yMax != bottom.y ||
        bottom.yMax != source.yMax || top.height + bottom.height != source.height || bottom.width != source.width ||
        left.x != source.x || left.y != source.y || left.height != source.height || left.xMax != right.x ||
        right.xMax != source.xMax || left.width + right.width != source.width || right.height != source.height)
        throw new System.InvalidOperationException("Split regions must cover the original rectangle without gaps/overlap.");
}
System.Console.WriteLine("PASS actual UnityStubs Rect partition invariants: both axes, zero/interior/full split, translated origin");
