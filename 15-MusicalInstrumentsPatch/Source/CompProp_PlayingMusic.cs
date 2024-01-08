using RimWorld;
using Verse;

namespace MusicPatch
{
    class CompProp_PlayingMusic : CompProperties
    {
        public SoundDef soundPlayInstrument;
        public CompProp_PlayingMusic()
        {
            this.compClass = typeof(Comp_PlayingMusic);
        }
    }
}