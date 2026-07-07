using System.Collections.Generic;

namespace UsefulStats.Core
{
    public sealed class MaterialVariantStat
    {
        public string Label = string.Empty;
        public float Count;
        public float WorkAmount;
        public float MarketValue;
        public bool IsDefault;
        public float MaterialPerWork => CraftableEfficiencyMetrics.Ratio(Count, WorkAmount);
        public float ValuePerWork => CraftableEfficiencyMetrics.Ratio(MarketValue, WorkAmount);
        public float ValuePerMaterial => CraftableEfficiencyMetrics.Ratio(MarketValue, Count);
    }

    public sealed class CraftableStatRow
    {
        public string Label = string.Empty;
        public string DefName = string.Empty;
        public string Source = string.Empty;
        public string KindKey = string.Empty;
        public string KindLabel = string.Empty;
        public bool AvailableNow;
        public bool FutureAvailable;
        public float WorkAmount;
        public float MarketValue;
        public string IngredientLabel = string.Empty;
        public float IngredientCount;
        public float IngredientPerWorkMin;
        public float IngredientPerWorkMax;
        public float DefaultValuePerMaterial;
        public int MaterialVariantCount;
        public string MaterialSummary = string.Empty;
        public string UnlockInfo = string.Empty;
        public string ExtraInfo = string.Empty;
        public List<MaterialVariantStat> MaterialVariants = new List<MaterialVariantStat>();

        public float ValuePerWork => CraftableEfficiencyMetrics.Ratio(MarketValue, WorkAmount);
        public float ValuePerMaterial => HasSingleIngredient ? DefaultValuePerMaterial : 0f;
        public float IngredientPerWork => HasSingleIngredient ? IngredientPerWorkMin : 0f;
        public bool HasSingleIngredient => !string.IsNullOrEmpty(MaterialSummary) && IngredientPerWorkMax > 0f;
        public bool HasExpandableMaterials => MaterialVariants.Count > 1;
        public string RowKey => Source + "|" + DefName;
    }
}
