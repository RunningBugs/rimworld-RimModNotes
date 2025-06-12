using Verse;
using RimWorld;

namespace RASL
{
	public class GameConditionModExtension : DefModExtension
	{
		[TranslationHandle]
		public string curse;

		public string letterLabel;
	}

	public class GameCondition_Curse : GameCondition_ForceWeather
	{
		public RAComponent Component
        {
            get
            {
				return Find.World.GetComponent<RAComponent>();
            }
        }

        public override void Init(){
            base.Init();
            Component.curse = "Wounded";
			var defExt = def.GetModExtension<GameConditionModExtension>();
			var label = defExt.letterLabel;

			var letter = LetterMaker.MakeLetter(label, def.letterText.Formatted(("RASL." + defExt.curse + "Desc").Translate().Named("CurseDesc")), def.letterDef);
			Find.LetterStack.ReceiveLetter(letter);
        }

		public override void End()
		{
			Component.curse = "Null";
			Find.LetterStack.ReceiveLetter("RASL.CurseEnded".Translate(), "RASL.CurseEndedDesc".Translate(), LetterDefOf.NeutralEvent);
			base.End();
			base.SingleMap.weatherDecider.StartNextWeather();
		}

		public override void ExposeData()
		{
			base.ExposeData();
		}
	}
}
