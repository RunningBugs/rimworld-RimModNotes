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
using HarmonyLib;

using LWM.DeepStorage;
using System.Reflection;


namespace DSSearchPatch
{
    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Harmony harmony = new Harmony("com.RunningBugs.DSSearchPatch");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(ITab_DeepStorage_Inventory), "FillTab")]
    public static class FillTabPatch
    {
        private static QuickSearchWidget searchWidget = new QuickSearchWidget();

        private static FieldInfo GetField(object __instance, string fieldName)
        {
            return AccessTools.Field(__instance.GetType(), fieldName);
        }

        private static PropertyInfo GetProperty(object __instance, string propertyName)
        {
            return AccessTools.Property(__instance.GetType(), propertyName);
        }

        private static MethodInfo GetMethod(object __instance, string methodName)
        {
            return AccessTools.Method(__instance.GetType(), methodName);
        }

        public static bool Prefix(ITab_DeepStorage_Inventory __instance)
        {
            // ref Building_Storage reference = ref buildingStorage;
            Thing selThing = (Thing)GetProperty(__instance, "SelThing").GetValue(__instance);
            Building_Storage reference = (Building_Storage)((selThing is Building_Storage) ? selThing : null);
            GetField(__instance, "buildingStorage").SetValue(__instance, reference);
            Building_Storage buildingStorage = reference;

            Vector2 size = (Vector2)GetField(__instance, "size").GetValue(__instance);

            if (buildingStorage != null)
            {
                Text.Font = GameFont.Small;
                Rect val = new(10f, 10f, size.x - 10f, size.y - 10f);
                GUI.BeginGroup(val);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                float num = 0f;
                Widgets.ListSeparator(ref num, val.width, __instance.labelKey.Translate());
                num += 5f;
                CompDeepStorage comp = buildingStorage.GetComp<CompDeepStorage>();
                List<Thing> source = comp == null ? ITab_Inventory_HeaderUtil.GenericContentsHeader(buildingStorage, out var header, out var tooltip) : comp.GetContentsHeader(out header, out tooltip);
                Rect val2 = new(8f, num, val.width - 16f, Text.CalcHeight(header, val.width - 16f));
                Widgets.Label(val2, header);
                num += val2.height;
                source = source.OrderBy((Thing x) => ((Def)x.def).defName).ThenByDescending(delegate (Thing x)
                {
                    QualityCategory val5 = new QualityCategory();
                    QualityUtility.TryGetQuality(x, out val5);
                    return val5;
                }).ThenByDescending((Thing x) => x.HitPoints / x.MaxHitPoints)
                    .ToList();

                Rect searchRect = new(8f, num, val.width - 24f, 30f);
                num += 30f;
                searchWidget.OnGUI(searchRect);

                Rect val3 = new Rect(0f, 10f + num, val.width, val.height - num);
                // Rect val4 = new(0f, 0f, val.width - 16f, scrollViewHeight);
                float scrollViewHeight = (float)GetField(__instance, "scrollViewHeight").GetValue(__instance);
                Rect val4 = new(0f, 0f, val.width - 16f, scrollViewHeight);

                Vector2 scrollPosition = (Vector2)GetField(__instance, "scrollPosition").GetValue(__instance);
                Widgets.BeginScrollView(val3, ref scrollPosition, val4, true);
                GetField(__instance, "scrollPosition").SetValue(__instance, scrollPosition);

                num = 0f;
                if (source.Count < 1)
                {
                    Widgets.Label(val4, Translator.Translate("NoItemsAreStoredHere"));
                    num += 22f;
                }
                else
                {
                    float ambientTemp = (float)GetField(__instance, "ambientTemp").GetValue(__instance);
                    ambientTemp = buildingStorage.AmbientTemperature;
                    GetField(__instance, "ambientTemp").SetValue(__instance, ambientTemp);
                }
                for (int i = 0; i < source.Count; i++)
                {
                    // DrawThingRow(ref num, val4.width, source[i]);
                    if (source[i].Label.Contains(searchWidget.filter.Text)) {
                        MethodInfo DrawThingRowMInfo = GetMethod(__instance, "DrawThingRow");
                        object[] parameters = new object[] { num, val4.width, source[i] };
                        DrawThingRowMInfo.Invoke(__instance, parameters);
                        num = (float)parameters[0];
                    }
                }
                if (Event.current.type == EventType.Layout)
                {
                    scrollViewHeight = num + 25f;
                    GetField(__instance, "scrollViewHeight").SetValue(__instance, scrollViewHeight);
                }
                Widgets.EndScrollView();
                GUI.EndGroup();
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            return false;
        }
    }

}
