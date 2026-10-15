using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;

// using System.Reflection;
using HarmonyLib;

namespace RestockingStatus
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        private static Texture2D restockingIcon = ContentFinder<Texture2D>.Get("RestockingStatus/refresh");
        private static Dictionary<Faction, Texture2D> factionRestockingIcons = new();

        static Start()
        {
            Log.Message("RestockingStatus loaded successfully!");
            Harmony harmony = new Harmony("com.RunningBugs.RestockingStatus");
            harmony.PatchAll(typeof(Start).Assembly);
        }

        public static Texture2D GetFactionRestockingIcon(Faction faction)
        {
            if (factionRestockingIcons.TryGetValue(faction, out Texture2D icon))
            {
                return icon;
            }

            Texture2D bg = faction?.def?.FactionIcon;
            if (bg == null)
            {
                return null;
            }
            Texture2D overlay = restockingIcon;
            Vector2 overlySize = new Vector2(bg.width / 1f, bg.height / 1f);
            Texture2D icon2 = TextureUtilities.OverlayTextureAtPosition(bg, overlay, overlySize);
            factionRestockingIcons[faction] = icon2;
            return icon2;
        }
    }


    public static class TextureUtilities
    {
        public static Texture2D FitToSize(Texture2D texture, Vector2 size)
        {
            int width = (int)size.x, height = (int)size.y;
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Texture2D result = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = texture.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        public static Texture2D TextureOverlay(Texture2D background, Texture2D overlay)
            => OverlayTextureAtPosition(background, overlay, new Vector2(background.width, background.height));

        public static Texture2D OverlayTextureAtPosition(Texture2D background, Texture2D overlay, Vector2 size)
        {
            Texture2D resized = FitToSize(overlay, size);
            try
            {
                Color[] pixels = background.GetPixels();
                Color[] layer = resized.GetPixels();
                int startX = background.width - resized.width;
                for (int y = 0; y < Math.Min(resized.height, background.height); y++)
                {
                    for (int x = 0; x < resized.width; x++)
                    {
                        int targetX = startX + x;
                        if (targetX < 0 || targetX >= background.width) continue;
                        int index = y * background.width + targetX;
                        Color source = layer[y * resized.width + x], target = pixels[index];
                        float alpha = source.a + target.a * (1f - source.a);
                        pixels[index] = alpha == 0f ? Color.clear : new Color(
                            (source.r * source.a + target.r * target.a * (1f - source.a)) / alpha,
                            (source.g * source.a + target.g * target.a * (1f - source.a)) / alpha,
                            (source.b * source.a + target.b * target.a * (1f - source.a)) / alpha, alpha);
                    }
                }
                Texture2D result = new Texture2D(background.width, background.height);
                result.SetPixels(pixels);
                result.Apply();
                return result;
            }
            finally { UnityEngine.Object.Destroy(resized); }
        }
    }


    [HarmonyPatch(typeof(Settlement), "ExpandingIcon", MethodType.Getter)]
    public static class Settlement_ExpandingIcon_Patch
    {
        public static void Postfix(Settlement __instance, ref Texture2D __result)
        {
            Faction faction = __instance.Faction;
            if (faction != null && faction != Faction.OfPlayer && !IsHostileToPlayer(faction))
            {
                //  nrt for Next Restock Tick
                var nrt = __instance.NextRestockTick;
                if (nrt != -1)
                {
                    // float daysToRestock = (nrt - Find.TickManager.TicksGame).TicksToDays();
                    if (nrt > Find.TickManager.TicksGame)
                    {
                        __result = Start.GetFactionRestockingIcon(faction);
                    }
                }
            }
        }

        // 某些派系（如美狐 Miho）与玩家未建立关系时，RelationWith 默认会报红字；
        // allowNull: true 静默返回 null，与原版 dummy relation 一样按非敌对处理。
        private static bool IsHostileToPlayer(Faction faction)
        {
            return faction.RelationWith(Faction.OfPlayer, allowNull: true)?.kind == FactionRelationKind.Hostile;
        }
    }
}
