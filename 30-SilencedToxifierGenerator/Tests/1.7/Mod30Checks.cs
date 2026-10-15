using System;
using Verse;
namespace RW17.Validation;
public static class Mod30Checks
{
    public const string PackageId = "RunningBugs.SilencedToxifierGenerator";
    public static void Run(Map map)
    {
        var sound = DefDatabase<SoundDef>.GetNamed("Toxifier_Working");
        if (sound.subSounds.Count == 0) throw new InvalidOperationException("Toxifier sub-sounds missing");
        foreach (var part in sound.subSounds)
            if (part.volumeRange.min != 0 || part.volumeRange.max != 0)
                throw new InvalidOperationException("Toxifier working sound is not silent");
    }
}
