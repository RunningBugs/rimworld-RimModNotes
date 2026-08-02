using RimWorld;
using Verse;

namespace ResearchPrerequisites
{
    /// <summary>
    /// 内部研究类别:普通 / 异象基础 / 异象高级。
    /// 原版用 knowledgeCategory == null 表示普通研究,但 null 作为语义值
    /// 容易引发事故(如 Dictionary 不允许 null 键),因此 Mod 内部一律
    /// 使用本枚举,仅在与原版 API 交互的边界处转换(见 ToVanillaDef)。
    /// </summary>
    public enum ResearchCategory
    {
        Normal = 0,
        AnomalyBasic = 1,
        AnomalyAdvanced = 2,
    }

    public static class ResearchCategories
    {
        public const int Count = 3;

        public static ResearchCategory Of(KnowledgeCategoryDef category)
        {
            if (category == KnowledgeCategoryDefOf.Basic)
            {
                return ResearchCategory.AnomalyBasic;
            }
            if (category == null)
            {
                return ResearchCategory.Normal;
            }
            return ResearchCategory.AnomalyAdvanced;
        }

        public static ResearchCategory Of(ResearchProjectDef project)
        {
            return Of(project?.knowledgeCategory);
        }

        /// <summary>
        /// 转换回原版 API 使用的 KnowledgeCategoryDef。
        /// 注意:普通研究在原版 API(如 ResearchManager.GetProject)中就是 null,
        /// 这是 Mod 内唯一允许 null 类别语义存在的地方(原版边界)。
        /// </summary>
        public static KnowledgeCategoryDef ToVanillaDef(ResearchCategory category)
        {
            switch (category)
            {
                case ResearchCategory.AnomalyBasic:
                    return KnowledgeCategoryDefOf.Basic;
                case ResearchCategory.AnomalyAdvanced:
                    return KnowledgeCategoryDefOf.Advanced;
                default:
                    return null;
            }
        }
    }
}
