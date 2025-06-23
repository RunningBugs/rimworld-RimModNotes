using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime;
using Verse;

namespace TunelerFix
{
    
     // This class will basically tell harmony to do it's thing so if you forget it the code below wont even run
    public class TunelerFix : Mod
    {
        public TunelerFix(ModContentPack content) : base(content)
        {
            Harmony harmony = new Harmony(content.PackageId);
            harmony.PatchAll();
        }
    }
   
    //the [HarmonyPatch] is a tag to tell harmony what to do with this part of the code, in this case use it to modify Rimworld code
    [HarmonyPatch]
    public static class XPFix
    {

        //this method  is use to point to the method you need to change, in my case it was a bit special as the method i am tageting is a nested one so i had to use AccessTools.Inner
        static MethodBase TargetMethod()
        {
             return AccessTools.Method(AccessTools.Inner(typeof(JobDriver_OperateDeepDrill), "<>c__DisplayClass1_0"), "<MakeNewToils>b__1");
        }
    

        static IEnumerable<CodeInstruction> Transpiler( IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {

            int insertTarget = 0;
            var codes = new List<CodeInstruction>(instructions);
            Label tagetJump = il.DefineLabel();

            //loop trought the list to find the line of code to change
            for (int i = 0; i < codes.Count; i++)
            {
                
                if (codes[i].opcode == OpCodes.Ldc_R4)
                {
                    var strOperand = (float)codes[i].operand;
                    string msg = strOperand.ToString();
                    Log.Warning(msg);
                    if (strOperand == 0.065f)
                    {
                        insertTarget = i - 3;
                        //label the return to allow jump to it later
                        codes[i + 3].labels.Add(tagetJump);
                        break;
                    }
                }
                //use 0.065 as an anchor as it is the only time it is use in the IL
                

            }

            //insert a conditional to the IL to prevent it for giving xp if the target does not have axp bar to begin with
            var instructionsToInsert = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn), nameof(Pawn.skills))),
                new CodeInstruction(OpCodes.Brfalse_S, tagetJump)
            };
    
            //check if the code was able to find the target, if yes inject the code else send an error
            if (insertTarget != 0)
                codes.InsertRange(insertTarget, instructionsToInsert);
            else
                Log.Error("TunnulerFix: Could not find the target code to modify");


            return codes;
        }
    }
}