using RimWorld;
using RimWorld.Planet;
using Verse;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;


namespace _RecipeBook
{
    [StaticConstructorOnStartup]
    public class RecipeBookLoading
    {
        public RecipeBookLoading()
        {
            Log.Message("RecipeBook loaded successfully!");
            var harmony = new Harmony("com.RunningBugs.RecipeBook");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    public class RecipeBookDatabase : GameComponent
    {
        public RecipeBookDatabase(Game game)
        {
        }
    }
}
