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
            harmony.PatchAll();
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
            RenderTexture rt = RenderTexture.GetTemporary(
                (int)size.x,
                (int)size.y,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Default
            );
            RenderTexture.active = rt;
            Graphics.Blit(texture, rt);
            Texture2D result = new Texture2D((int)size.x, (int)size.y);
            result.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            result.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        public static Texture2D TextureOverlay(Texture2D background, Texture2D overlay)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                background.width,
                background.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Default
            );
            RenderTexture.active = rt;
            Graphics.Blit(background, rt);
            Graphics.Blit(overlay, rt);
            Texture2D result = new Texture2D(background.width, background.height);
            result.ReadPixels(new Rect(0, 0, background.width, background.height), 0, 0);
            result.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        public static Texture2D OverlayTextureAtPosition(Texture2D background, Texture2D overlay, Vector2 size)
        {
            Texture2D resizedOverlay = FitToSize(overlay, size);
            Vector2 position = new(
                background.width - size.x,
                background.height - size.y
            );

            RenderTexture rt = RenderTexture.GetTemporary(background.width, background.height);
            RenderTexture.active = rt;
            Graphics.Blit(background, rt);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, background.width, background.height, 0);
            Graphics.DrawTexture(new Rect(position.x, position.y, size.x, size.y), resizedOverlay);
            GL.PopMatrix();

            Texture2D result = new(background.width, background.height);
            result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            result.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }
    }


    [HarmonyPatch(typeof(Settlement), "ExpandingIcon", MethodType.Getter)]
    public static class Settlement_ExpandingIcon_Patch
    {
        public static void Postfix(Settlement __instance, ref Texture2D __result)
        {
            if (__instance.Faction != Faction.OfPlayer && !__instance.Faction.HostileTo(Faction.OfPlayer))
            {
                //  nrt for Next Restock Tick
                var nrt = __instance.NextRestockTick;
                if (nrt != -1)
                {
                    // float daysToRestock = (nrt - Find.TickManager.TicksGame).TicksToDays();
                    if (nrt > Find.TickManager.TicksGame)
                    {
                        __result = Start.GetFactionRestockingIcon(__instance.Faction);
                    }
                }
            }
        }
    }
}
