using RimWorld;
using Verse;

namespace Blueprint2;

[DefOf]
public static class BlueprintKeyBindingDefOf
{
    public static KeyBindingDef Blueprint_MirrorX;
    public static KeyBindingDef Blueprint_MirrorZ;
    public static KeyBindingDef Blueprint_SwitchMode;

    static BlueprintKeyBindingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(BlueprintKeyBindingDefOf));
    }
}