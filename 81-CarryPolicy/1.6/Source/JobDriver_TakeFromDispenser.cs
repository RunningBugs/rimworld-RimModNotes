using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace _CarryPolicy;

// 走到营养膏机（含 mod 子类）取一份餐放进背包（一次一份，避免一次清空料斗）
public class JobDriver_TakeFromDispenser : JobDriver
{
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDespawnedOrNull(TargetIndex.A);
		this.FailOn(() => !(job.targetA.Thing is Building_NutrientPasteDispenser dispenser) || !dispenser.CanDispenseNow);
		yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
		Toil toil = ToilMaker.MakeToil("CarryPolicyTakeFromDispenser");
		toil.initAction = delegate
		{
			Pawn actor = toil.actor;
			Thing thing = ((Building_NutrientPasteDispenser)actor.CurJob.GetTarget(TargetIndex.A).Thing).TryDispenseFood();
			if (thing == null)
			{
				actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
			}
			else if (!actor.inventory.innerContainer.TryAddOrTransfer(thing))
			{
				thing.Destroy();
				actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
			}
		};
		toil.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
		toil.defaultCompleteMode = ToilCompleteMode.Delay;
		toil.defaultDuration = Building_NutrientPasteDispenser.CollectDuration;
		yield return toil;
	}
}
