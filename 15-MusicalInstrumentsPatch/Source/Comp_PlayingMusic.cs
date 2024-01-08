using Verse;
using RimWorld;
using Verse.Sound;
using System.Linq;
using System.Collections.Generic;

namespace MusicPatch
{
    class Comp_PlayingMusic : ThingComp
    {
        public static Dictionary<Pawn, Comp_PlayingMusic> notebook = new Dictionary<Pawn, Comp_PlayingMusic>();
        private Pawn currentPlayer;
        private Sustainer soundPlaying;
        public CompProp_PlayingMusic Props => (CompProp_PlayingMusic)props;

        public void StartPlaying(Pawn player)
        {
            currentPlayer = player;
            notebook.Add(player, this);
        }

        public void StopPlaying(Pawn pawn)
        {
            currentPlayer = null;
            notebook.Remove(pawn);
        }

        public override void CompTick()
        {
            if (currentPlayer != null)
            {
                if (Props.soundPlayInstrument != null && soundPlaying == null)
                {
                    soundPlaying = Props.soundPlayInstrument.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(currentPlayer.Position, currentPlayer.Map), MaintenanceType.PerTick));
                }
            }
            else
            {
                soundPlaying = null;
            }
            soundPlaying?.Maintain();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.ShowDevGizmos)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = $"DEV: Toggle is playing, status: {currentPlayer != null}";
                command_Action.action = delegate
                {
                    currentPlayer = ((currentPlayer == null) ? PawnsFinder.AllMaps_FreeColonists.FirstOrDefault() : null);
                };
                yield return command_Action;
            }
        }
    }
}