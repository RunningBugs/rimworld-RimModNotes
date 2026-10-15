using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RW17.Validation;

public static class Mod05Checks
{
    public const string PackageId = "RunningBugs.HighlightEnemies";
    public static void Run(Map map)
    {
        var context = SynchronizationContext.Current as Waylith.MainThreadSynchronizationContext;
        Require(context != null && context.IsMainThread, "Run after map initialization on the Waylith main thread");
        var type = AccessTools.TypeByName("_HightlightEnemies.EnemyHighlighter");
        var component = map.components.SingleOrDefault(c => c.GetType() == type);
        Require(component != null, "MapComponent missing");
        var target = AccessTools.Method(typeof(PlaySettings), "DoPlaySettingsGlobalControls");
        Require(Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName
            == "_HightlightEnemies.EnemyHighlighter+ToggleIconPatcher" && p.PatchMethod.Name == "AddIcon") == true,
            "Concrete toolbar postfix missing");
        var taskField = AccessTools.Field(type, "lastTask");
        Finish((Task)taskField.GetValue(component));
        var enabled = type.GetField("markEnemies");
        var settings = AccessTools.TypeByName("_HightlightEnemies.HE_ModSettings");
        var fog = settings.GetField("markEnemiesInFog");
        var invisible = settings.GetField("markEnemiesInvisible");
        var oldEnabled = enabled.GetValue(null);
        var oldFog = fog.GetValue(null);
        var oldInvisible = invisible.GetValue(null);
        var mark = DefDatabase<DesignationDef>.GetNamed("HE_Mark");
        var manager = map.designationManager;
        var oldTargets = manager.AllDesignations.Where(d => d.def == mark).Select(d => d.target).ToArray();
        Pawn enemy = null;
        Rand.PushState(17);
        try
        {
            enabled.SetValue(null, true);
            fog.SetValue(null, true);
            invisible.SetValue(null, true);
            var faction = Find.FactionManager.AllFactions.First(f => f.def.humanlikeFaction
                && f.HostileTo(Faction.OfPlayer));
            var colonist = map.mapPawns.FreeColonists.First();
            var cell = GenRadial.RadialCellsAround(colonist.Position, 20f, false)
                .First(c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map)
                    && c.GetThingList(map).Count == 0);
            enemy = PawnGenerator.GeneratePawn(PawnKindDefOf.SpaceRefugee, faction);
            GenSpawn.Spawn(enemy, cell, map);
            Require(GenHostility.HostileTo(enemy, Faction.OfPlayer)
                && map.attackTargetsCache.TargetsHostileToColony.Any(t => t.Thing == enemy),
                "Real hostile target cache input was not established");
            component.MapComponentTick();
            Finish((Task)taskField.GetValue(component));
            Require(manager.DesignationOn(enemy, mark) != null, "Real tick/async continuation did not mark the enemy");
            component.MapComponentTick();
            Finish((Task)taskField.GetValue(component));
            Require(manager.AllDesignations.Count(d => d.def == mark && d.target.Thing == enemy) == 1,
                "Repeated highlighting added duplicate designations");
            enabled.SetValue(null, false);
            component.MapComponentTick();
            Require(!manager.AllDesignations.Any(d => d.def == mark), "Disabled tick did not clear highlighting");
        }
        finally
        {
            if (enemy != null && !enemy.Destroyed) enemy.Destroy(DestroyMode.Vanish);
            manager.RemoveAllDesignationsOfDef(mark);
            foreach (var oldTarget in oldTargets) manager.AddDesignation(new Designation(oldTarget, mark));
            enabled.SetValue(null, oldEnabled);
            fog.SetValue(null, oldFog);
            invisible.SetValue(null, oldInvisible);
            Rand.PopState();
        }
        void Finish(Task task)
        {
            var watch = Stopwatch.StartNew();
            while (!task.IsCompleted && watch.ElapsedMilliseconds < 5000)
            {
                // Reuse the engine's actual main-thread queue; blocking Task.Wait would deadlock.
                context.Pump();
                Thread.Sleep(1);
            }
            Require(task.IsCompleted, "Async highlighting did not finish within five seconds");
            task.GetAwaiter().GetResult();
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Mod05: " + message);
    }
}
