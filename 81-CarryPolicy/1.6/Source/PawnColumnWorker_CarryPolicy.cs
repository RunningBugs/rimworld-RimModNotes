using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace _CarryPolicy;

public static class CarryPolicyUIUtility
{
	public static void DoAssignCarryPolicyButtons(Rect rect, Pawn pawn)
	{
		CarryPolicy carryPolicy = CarryPolicyUtility.CurrentPolicyFor(pawn);
		if (carryPolicy == null)
		{
			return;
		}
		Rect rect2 = rect.ContractedBy(0f, 2f);
		Widgets.Dropdown(rect2, pawn, (Pawn p) => CarryPolicyUtility.CurrentPolicyFor(p), GenerateMenu, carryPolicy.label.Truncate(rect2.width), null, carryPolicy.label, null, null, paintable: true);
	}

	private static IEnumerable<Widgets.DropdownMenuElement<Policy>> GenerateMenu(Pawn pawn)
	{
		foreach (CarryPolicy allPolicy in CarryPolicyDatabase.Instance.AllPolicies)
		{
			CarryPolicy policy = allPolicy;
			yield return new Widgets.DropdownMenuElement<Policy>
			{
				option = new FloatMenuOption(policy.label, delegate
				{
					CarryPolicyUtility.SetPolicyFor(pawn, policy);
				}),
				payload = policy
			};
		}
		yield return new Widgets.DropdownMenuElement<Policy>
		{
			option = new FloatMenuOption(string.Format("{0}...", "AssignTabEdit".Translate()), delegate
			{
				Find.WindowStack.Add(new Dialog_ManageCarryPolicies(CarryPolicyUtility.CurrentPolicyFor(pawn)));
			})
		};
	}
}

public class PawnColumnWorker_CarryPolicy : PawnColumnWorker
{
	private const int TopAreaHeight = 65;

	public override void DoHeader(Rect rect, PawnTable table)
	{
		base.DoHeader(rect, table);
		MouseoverSounds.DoRegion(rect);
		Rect rect2 = new Rect(rect.x, rect.y + (rect.height - 65f), Mathf.Min(rect.width, 360f), 32f);
		if (Widgets.ButtonText(rect2, "ManageCarryPolicies".Translate()))
		{
			Find.WindowStack.Add(new Dialog_ManageCarryPolicies(null));
		}
	}

	public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
	{
		if (pawn.IsColonist)
		{
			CarryPolicyUIUtility.DoAssignCarryPolicyButtons(rect, pawn);
		}
	}

	public override int GetMinWidth(PawnTable table)
	{
		return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(194f));
	}

	public override int GetOptimalWidth(PawnTable table)
	{
		return Mathf.Clamp(Mathf.CeilToInt(251f), GetMinWidth(table), GetMaxWidth(table));
	}

	public override int GetMinHeaderHeight(PawnTable table)
	{
		return Mathf.Max(base.GetMinHeaderHeight(table), TopAreaHeight);
	}

	public override int Compare(Pawn a, Pawn b)
	{
		return GetValueToCompare(a).CompareTo(GetValueToCompare(b));
	}

	private int GetValueToCompare(Pawn pawn)
	{
		CarryPolicy carryPolicy = CarryPolicyUtility.CurrentPolicyFor(pawn);
		return (carryPolicy != null) ? carryPolicy.id : int.MinValue;
	}
}
