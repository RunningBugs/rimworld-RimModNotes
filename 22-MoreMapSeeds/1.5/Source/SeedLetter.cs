using Verse;
using RimWorld;
using UnityEngine;

namespace MoreMapSeeds
{
    [DefOf]
	public static class MyLetterDefOf
	{
		public static LetterDef NeutralEventCopyLetter;
	}

	public class WorldSeedGameComponent : GameComponent
	{
		public WorldSeedGameComponent(Game game) : base()
		{
		}

        public override void FinalizeInit()
        {
            base.FinalizeInit();
			// Log.Warning($"[MoreMapSeeds] FinalizeInit");
			var seed = Find.World.info.seedString;
            Find.LetterStack.ReceiveLetter("WorldSeedLtterTitle".Translate(seed), seed, MyLetterDefOf.NeutralEventCopyLetter);
        }
    }

	public class StandardLetterCopyOnClick : StandardLetter
	{
		public override void OpenLetter()
		{
			var text = Text.Resolve();
			GUIUtility.systemCopyBuffer = text;
			Messages.Message("WorldSeedCopied".Translate(text), MessageTypeDefOf.NeutralEvent);
			base.OpenLetter();
		}
	}
}