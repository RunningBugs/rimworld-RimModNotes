using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
namespace RW17.Validation;
public static class Mod19Checks
{
    public const string PackageId = "RunningBugs.FacialAnimationEyeFix";
    private static int repaints;
    public static void Observe() { repaints++; }
    public static void Run(Map map)
    {
        var upstream = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "FacialAnimation");
        var pool = upstream.GetType("FacialAnimation.MyGraphicPool", true);
        var repaint = pool.GetMethod("RepaintAllGraphic", BindingFlags.Static | BindingFlags.Public);
        var own = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "FacialAnimationEyeFix");
        var componentType = own.GetType("FacialAnimationEyeFix.TextureReDraw", true);
        if (!Current.Game.components.Any(c => c.GetType() == componentType)) throw new Exception("Mod19: real game component was not discovered");
        var fixture = (GameComponent)Activator.CreateInstance(componentType, Current.Game);
        var harmony = new Harmony("RW17.Validation.Mod19.Observe");
        var observer = AccessTools.Method(typeof(Mod19Checks), nameof(Observe));
        int count = (int)pool.GetMethod("Count").Invoke(null, null);
        try
        {
            repaints = 0; harmony.Patch(repaint, prefix: new HarmonyMethod(observer));
            fixture.GameComponentTick();
            if (repaints != 1) throw new Exception("Mod19: fresh component did not invoke actual upstream repaint");
            fixture.GameComponentTick();
            if (repaints != 1) throw new Exception("Mod19: repaint was not once-per-component");
        }
        finally { harmony.Unpatch(repaint, observer); }
        Console.WriteLine("RW17_MOD19_ACTUAL_REPAINT_LIFECYCLE_OK; pool=" + count + "; visual black-eye correction/rendered pixels remain pending");
    }
}
