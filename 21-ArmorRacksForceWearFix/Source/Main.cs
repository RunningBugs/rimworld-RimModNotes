using Verse;
using RimWorld;

using HarmonyLib;
using Logs = Logger.Log;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using ArmorRacks;
using System.Linq;
using Unity.Jobs.LowLevel.Unsafe;

namespace Template
{

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            Harmony harmony = new Harmony("com.runningbugs.ArmorRacksPatches");
            harmony.PatchAll();
            Logs.Message("loaded!");
        }
    }

    [HarmonyPatch]
    class ArmorRacksPatches
    {
        public static MethodBase TargetMethod()
        {
            var containingType = typeof(ArmorRacks.Jobs.JobDriverTransferToRack);
            return containingType.GetMethod("<MakeNewToils>b__1_0", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static void Action(Pawn pawn, Apparel apparel)
        {
            if (LoadedModManager.GetMod<ArmorRacksMod>().GetSettings<ArmorRacksModSettings>().EquipSetForced)
            {
                pawn.outfits.forcedHandler.SetForced(apparel, true);
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int pos = -1;

            for (int i = 0; i < codes.Count; i++)
            {
                var instruction = codes[i];

                if (instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo method && method.Name == "Wear" && method.DeclaringType == typeof(Pawn_ApparelTracker))
                {
                    pos = i + 1;
                    break;
                }
            }

            if (pos != -1)
            {
                codes.Insert(pos++, new CodeInstruction(OpCodes.Ldarg_0)); //  load the instance
                FieldInfo pawnField = AccessTools.Field(typeof(ArmorRacks.Jobs.JobDriverTransferToRack), "pawn");
                codes.Insert(pos++, new CodeInstruction(OpCodes.Ldfld, pawnField)); //  load the pawn

                codes.Insert(pos++, new CodeInstruction(OpCodes.Ldloc_S, 17));  //  load the apparel

                var callActionCode = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArmorRacksPatches), "Action"));
                codes.Insert(pos++, callActionCode);                //  call the method to consume the object
            }

            return codes;
        }
    }

}
