using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    /// <summary>
    /// 任何路径(原版按钮、本 Mod、其他 Mod)使项目成为当前研究后,
    /// 把它挪到对应队列队首,维持"当前研究 = 队首"的不变量。
    /// 本 Mod 自己的启动路径也走 SetCurrentProject,此同步为幂等 no-op。
    /// </summary>
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject))]
    public static class SetCurrentProjectPatch
    {
        public static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null || Scribe.mode != LoadSaveMode.Inactive)
            {
                return;
            }
            ResearchQueue.Instance?.MoveToHead(proj);
        }
    }
}
