using System;
using System.Collections.Generic;
using System.Linq;
using UsefulStats.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace UsefulStats
{
    public static class CraftableRowBuilder
    {
        private const int MaxMaterialVariantsForSummary = 256;

        private sealed class MaterialCandidate
        {
            public ThingDef ThingDef;
            public float Count;
            public float Work;
            public float Value;
            public bool IsDefault;
            public string Note = string.Empty;
            public float IngredientPerWork => CraftableEfficiencyMetrics.Ratio(Count, Work);
        }

        private sealed class KindInfo
        {
            public string Key;
            public string Label;
        }

        public static List<CraftableStatRow> BuildRows(Map map)
        {
            var rows = new List<CraftableStatRow>();
            var seen = new HashSet<string>();

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe == null || recipe.products.NullOrEmpty() || recipe.ProducedThingDef == null || recipe.IsSurgery)
                {
                    continue;
                }

                ThingDef product = recipe.ProducedThingDef;
                if (product.category != ThingCategory.Item && product.category != ThingCategory.Building)
                {
                    continue;
                }

                bool anyPotentialUser = HasPotentialRecipeUser(recipe);
                bool anyUserNow = HasAvailableRecipeUser(recipe, map);
                bool now = SafeAvailableNow(recipe) && anyUserNow;
                string unlock = BuildRecipeUnlockInfo(recipe, anyUserNow);
                CraftableStatRow row = RowForRecipe(recipe, product, now, !now && anyPotentialUser, unlock);
                string key = "recipe|" + recipe.defName + "|" + product.defName;
                if (seen.Add(key)) rows.Add(row);
            }

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null || !def.BuildableByPlayer || def.category != ThingCategory.Building)
                {
                    continue;
                }

                ThingDef defaultStuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                float work = def.GetStatValueAbstract(StatDefOf.WorkToBuild, defaultStuff);
                if (work <= 0f)
                {
                    continue;
                }

                bool now = def.IsResearchFinished;
                CraftableStatRow row = RowForBuildable(def, now, !now && !def.researchPrerequisites.NullOrEmpty(), BuildableUnlockInfo(def));
                string key = "build|" + def.defName;
                if (seen.Add(key)) rows.Add(row);
            }

            return rows.OrderByDescending(r => r.AvailableNow).ThenBy(r => r.KindLabel).ThenBy(r => r.Label).ToList();
        }

        private static CraftableStatRow RowForRecipe(RecipeDef recipe, ThingDef product, bool now, bool future, string unlock)
        {
            ThingDef defaultStuff = product.MadeFromStuff ? GenStuff.DefaultStuffFor(product) : null;
            float work = recipe.WorkAmountForStuff(defaultStuff);
            float value = product.GetStatValueAbstract(StatDefOf.MarketValue, defaultStuff);
            var candidates = SingleIngredientCandidates(recipe).ToList();
            KindInfo kind = KindFor(product);
            CraftableStatRow row = new CraftableStatRow
            {
                Label = ProductLabel(product, defaultStuff),
                DefName = product.defName,
                Source = recipe.label.NullOrEmpty() ? recipe.defName : recipe.LabelCap.ToString(),
                KindKey = kind.Key,
                KindLabel = kind.Label,
                AvailableNow = now,
                FutureAvailable = future,
                WorkAmount = work,
                MarketValue = value,
                UnlockInfo = unlock
            };
            ApplyMaterialSummary(row, candidates, product.MadeFromStuff ? "stuff" : "ingredient");
            return row;
        }

        private static CraftableStatRow RowForBuildable(ThingDef def, bool now, bool future, string unlock)
        {
            ThingDef defaultStuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            var candidates = SingleBuildIngredient(def).ToList();
            KindInfo kind = KindFor(def);
            CraftableStatRow row = new CraftableStatRow
            {
                Label = ProductLabel(def, defaultStuff),
                DefName = def.defName,
                Source = "Build",
                KindKey = kind.Key,
                KindLabel = kind.Label,
                AvailableNow = now,
                FutureAvailable = future,
                WorkAmount = def.GetStatValueAbstract(StatDefOf.WorkToBuild, defaultStuff),
                MarketValue = def.GetStatValueAbstract(StatDefOf.MarketValue, defaultStuff),
                UnlockInfo = unlock
            };
            ApplyMaterialSummary(row, candidates, def.MadeFromStuff ? "stuff" : "ingredient");
            return row;
        }

        private static void ApplyMaterialSummary(CraftableStatRow row, List<MaterialCandidate> candidates, string variantKind)
        {
            if (candidates.Count == 0)
            {
                row.MaterialSummary = string.Empty;
                row.IngredientLabel = string.Empty;
                row.IngredientCount = 0f;
                row.IngredientPerWorkMin = 0f;
                row.IngredientPerWorkMax = 0f;
                row.MaterialVariantCount = 0;
                return;
            }

            MaterialCandidate best = candidates.OrderBy(c => c.IngredientPerWork).First();
            MaterialCandidate worst = candidates.OrderByDescending(c => c.IngredientPerWork).First();
            row.IngredientLabel = best.ThingDef?.LabelCap.ToString() ?? string.Empty;
            row.IngredientCount = best.Count;
            row.IngredientPerWorkMin = best.IngredientPerWork;
            row.IngredientPerWorkMax = worst.IngredientPerWork;
            MaterialCandidate defaultCandidate = candidates.FirstOrDefault(c => c.IsDefault) ?? best;
            row.DefaultValuePerMaterial = CraftableEfficiencyMetrics.Ratio(defaultCandidate.Value, defaultCandidate.Count);
            row.MaterialVariantCount = candidates.Count;
            row.MaterialVariants = candidates.Select(c => new MaterialVariantStat
            {
                Label = c.ThingDef?.LabelCap.ToString() ?? string.Empty,
                Count = c.Count,
                WorkAmount = c.Work,
                MarketValue = c.Value,
                IsDefault = c == defaultCandidate
            }).OrderBy(v => v.Label).ToList();

            string countText = FormatCountRange(candidates.Min(c => c.Count), candidates.Max(c => c.Count));
            if (candidates.Count == 1)
            {
                row.MaterialSummary = countText + " x " + row.IngredientLabel;
                row.ExtraInfo = best.Note;
            }
            else
            {
                row.MaterialSummary = countText + " x " + variantKind + " (" + candidates.Count + ")";
                row.ExtraInfo = "Best material/work: " + best.ThingDef.LabelCap + " " + CraftableEfficiencyMetrics.FormatRatio(best.IngredientPerWork) +
                                "\nWorst material/work: " + worst.ThingDef.LabelCap + " " + CraftableEfficiencyMetrics.FormatRatio(worst.IngredientPerWork) +
                                "\nAggregated into one row to avoid thousands of material-variant rows.";
            }
        }

        private static string FormatCountRange(float min, float max)
        {
            string a = min.ToString("0.##");
            string b = max.ToString("0.##");
            return a == b ? a : a + "-" + b;
        }

        private static IEnumerable<MaterialCandidate> SingleIngredientCandidates(RecipeDef recipe)
        {
            if (recipe.ingredients == null || recipe.ingredients.Count != 1)
            {
                yield break;
            }

            IngredientCount ingredient = recipe.ingredients[0];
            ThingDef product = recipe.ProducedThingDef;
            ThingDef defaultStuff = product.MadeFromStuff ? GenStuff.DefaultStuffFor(product) : null;
            IEnumerable<ThingDef> allowed = ingredient.filter.AllowedThingDefs.Where(td => recipe.fixedIngredientFilter == null || recipe.fixedIngredientFilter.Allows(td));
            if (product.MadeFromStuff)
            {
                allowed = allowed.Where(td => td.IsStuff && td.stuffProps != null && !product.stuffCategories.NullOrEmpty() && td.stuffProps.categories.Any(c => product.stuffCategories.Contains(c)));
            }

            foreach (ThingDef td in allowed.OrderBy(td => td.label).Take(MaxMaterialVariantsForSummary))
            {
                float count = ingredient.CountRequiredOfFor(td, recipe);
                ThingDef stuff = product.MadeFromStuff ? td : null;
                yield return new MaterialCandidate
                {
                    ThingDef = td,
                    Count = count,
                    Work = recipe.WorkAmountForStuff(stuff),
                    Value = product.GetStatValueAbstract(StatDefOf.MarketValue, stuff),
                    IsDefault = td == defaultStuff || !product.MadeFromStuff,
                    Note = ingredient.IsFixedIngredient ? string.Empty : "variable ingredient"
                };
            }
        }

        private static IEnumerable<MaterialCandidate> SingleBuildIngredient(ThingDef def)
        {
            bool hasStuff = def.MadeFromStuff && def.CostStuffCount > 0;
            bool hasFixedCosts = !def.CostList.NullOrEmpty();
            ThingDef defaultStuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            if (hasStuff && !hasFixedCosts)
            {
                foreach (ThingDef stuff in DefDatabase<ThingDef>.AllDefsListForReading.Where(td => td.IsStuff && td.stuffProps != null && td.stuffProps.categories.Any(c => def.stuffCategories.Contains(c))).OrderBy(td => td.label).Take(MaxMaterialVariantsForSummary))
                {
                    yield return new MaterialCandidate
                    {
                        ThingDef = stuff,
                        Count = def.CostStuffCount,
                        Work = def.GetStatValueAbstract(StatDefOf.WorkToBuild, stuff),
                        Value = def.GetStatValueAbstract(StatDefOf.MarketValue, stuff),
                        IsDefault = stuff == defaultStuff,
                        Note = "variable stuff"
                    };
                }
            }
            else if (!hasStuff && def.CostList != null && def.CostList.Count == 1)
            {
                ThingDefCountClass cost = def.CostList[0];
                yield return new MaterialCandidate
                {
                    ThingDef = cost.thingDef,
                    Count = cost.count,
                    Work = def.GetStatValueAbstract(StatDefOf.WorkToBuild),
                    Value = def.GetStatValueAbstract(StatDefOf.MarketValue),
                    IsDefault = true
                };
            }
        }

        private static bool SafeAvailableNow(RecipeDef recipe)
        {
            try { return recipe.AvailableNow; }
            catch (Exception) { return false; }
        }

        private static bool HasPotentialRecipeUser(RecipeDef recipe)
        {
            return recipe.AllRecipeUsers.Any(user => user.category != ThingCategory.Pawn);
        }

        private static bool HasAvailableRecipeUser(RecipeDef recipe, Map map)
        {
            if (map == null)
            {
                return recipe.AllRecipeUsers.Any(user => user.category != ThingCategory.Pawn);
            }

            foreach (ThingDef user in recipe.AllRecipeUsers)
            {
                if (user.category == ThingCategory.Pawn) continue;
                if (user.IsResearchFinished)
                {
                    if (user.building == null || map.listerBuildings == null) return true;
                    if (map.listerBuildings.AllBuildingsColonistOfDef(user).Any(b => recipe.AvailableOnNow(b))) return true;
                }
            }
            return false;
        }

        private static string BuildRecipeUnlockInfo(RecipeDef recipe, bool anyUserNow)
        {
            var parts = new List<string>();
            if (!anyUserNow) parts.Add("needs bench/user");
            if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished) parts.Add(recipe.researchPrerequisite.LabelCap.ToString());
            if (recipe.researchPrerequisites != null) parts.AddRange(recipe.researchPrerequisites.Where(r => !r.IsFinished).Select(r => r.LabelCap.ToString()));
            if (recipe.memePrerequisitesAny != null && recipe.memePrerequisitesAny.Count > 0) parts.Add("ideo meme");
            if (recipe.factionPrerequisiteTags != null && recipe.factionPrerequisiteTags.Count > 0) parts.Add("faction tag");
            if (recipe.fromIdeoBuildingPreceptOnly) parts.Add("ideo precept");
            return parts.Distinct().ToCommaList();
        }

        private static string BuildableUnlockInfo(ThingDef def)
        {
            if (def.researchPrerequisites.NullOrEmpty()) return string.Empty;
            return def.researchPrerequisites.Where(r => !r.IsFinished).Select(r => r.LabelCap.ToString()).ToCommaList();
        }

        private static KindInfo KindFor(ThingDef def)
        {
            ThingCategoryDef storageRoot = null;
            if (!def.thingCategories.NullOrEmpty())
            {
                storageRoot = def.thingCategories
                    .Select(StorageRootCategory)
                    .Where(cat => cat != null)
                    .OrderBy(cat => cat.label)
                    .FirstOrDefault();
            }

            if (storageRoot == null && def.category == ThingCategory.Building)
            {
                storageRoot = ThingCategoryDefOf.Buildings;
            }

            if (storageRoot != null)
            {
                string label = storageRoot.LabelCap.ToString();
                return new KindInfo { Key = storageRoot.defName, Label = label.NullOrEmpty() ? storageRoot.defName : label };
            }

            string fallback = def.category.ToString();
            return new KindInfo { Key = fallback, Label = fallback };
        }

        private static ThingCategoryDef StorageRootCategory(ThingCategoryDef category)
        {
            if (category == null) return null;
            ThingCategoryDef current = category;
            while (current.parent != null && current.parent != ThingCategoryDefOf.Root)
            {
                current = current.parent;
            }
            return current.parent == ThingCategoryDefOf.Root ? current : category;
        }

        private static string ProductLabel(ThingDef def, ThingDef stuff)
        {
            if (stuff == null) return def.LabelCap.ToString();
            return (stuff.label + " " + def.label).CapitalizeFirst();
        }
    }
}
