using HarmonyLib;
using RimWorld;
using TacticalGroups;
using UnityEngine;
using Verse;

namespace CGTargetablePortraits
{
    [StaticConstructorOnStartup]
    public static class ModEntry
    {
        static ModEntry()
        {
            new Harmony("RunningBugs.ColonyGroupsTargetablePortraits").PatchAll();
        }
    }

    /// <summary>
    /// [LTO] Colony Groups 替换了 vanilla ColonistBar 的绘制，但没有 patch
    /// ColonistBar.TryGetEntryAt，导致 Targeter 选目标时无法命中 TG 的头像。
    /// 本 patch 把命中测试转发给 TG 自己的实现：主栏 TryGetEntryAt，
    /// 失败则 fallback 到悬停组弹窗的 TryGetGroupPawnAt。
    /// </summary>
    [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.TryGetEntryAt))]
    public static class ColonistBarTryGetEntryAtPatch
    {
        public static bool Prefix(Vector2 pos, out ColonistBar.Entry entry, ref bool __result)
        {
            TacticalColonistBar bar = TacticUtils.TacticalColonistBar;
            if (bar == null)
            {
                // TG 未就绪（如主菜单阶段）：放行原逻辑
                entry = default;
                return true;
            }
            if (bar.TryGetEntryAt(pos, out TacticalColonistBar.Entry tgEntry))
            {
                entry = new ColonistBar.Entry(tgEntry.pawn, tgEntry.map, tgEntry.group);
                __result = true;
                return false;
            }
            if (bar.TryGetGroupPawnAt(pos, out Pawn popupPawn))
            {
                entry = new ColonistBar.Entry(popupPawn, popupPawn.Map, 0);
                __result = true;
                return false;
            }
            // TG 环境下 vanilla 命中测试本就无意义，直接不命中
            entry = default;
            __result = false;
            return false;
        }
    }
}
