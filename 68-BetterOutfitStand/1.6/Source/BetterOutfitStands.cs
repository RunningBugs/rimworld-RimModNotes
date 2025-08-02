using System.Collections.Generic;
using Verse;
using RimWorld;

namespace BetterOutfitStand;

/// <summary>
/// Extended version of the vanilla Building_OutfitStand
/// </summary>
public class Building_BetterOutfitStand : Building_OutfitStand
{
    public HashSet<Apparel> WornApparelToTransferToStand = new();

    public HashSet<Apparel> StandApparelToTransferToPawn = new();

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
    {
        // Add our custom options here, which will open a window
        if (selPawn.apparel.AnyApparel || HeldItems.Count > 0)
        {
            yield return new FloatMenuOption("BetterOutfitStand".Translate(), () =>
            {
                WornApparelToTransferToStand.Clear();
                StandApparelToTransferToPawn.Clear();
                Find.WindowStack.Add(new Dialog_ChooseApparel(selPawn, this));
            });
        }

        foreach (var option in base.GetFloatMenuOptions(selPawn))
        {
            yield return option;
        }
    }
}

public class Building_BetterKidOutfitStand : Building_BetterOutfitStand
{
    protected override BodyTypeDef BodyTypeDefForRendering { get; } = BodyTypeDefOf.Child;

	protected override float WeaponDrawDistanceFactor => 0.55f;
}