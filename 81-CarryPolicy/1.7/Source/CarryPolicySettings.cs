using System.Collections.Generic;
using RimWorld;
using Verse;

namespace _CarryPolicy;

public class CrossSaveCarryEntryRecord : IExposable
{
	public string thing;

	public float count = 1f;

	public void ExposeData()
	{
		Scribe_Values.Look(ref thing, "thing");
		Scribe_Values.Look(ref count, "count", 1f);
	}
}

public class CrossSaveCarryPolicyRecord : IExposable
{
	public int id;

	public string label;

	public List<CrossSaveCarryEntryRecord> entries = new List<CrossSaveCarryEntryRecord>();

	public void ExposeData()
	{
		Scribe_Values.Look(ref id, "id", 0);
		Scribe_Values.Look(ref label, "label");
		Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			entries ??= new List<CrossSaveCarryEntryRecord>();
		}
	}
}

public class CarryPolicySettings : ModSettings
{
	public List<CarryPolicy> defaultPolicies = new List<CarryPolicy>();

	private List<CrossSaveCarryPolicyRecord> scribedRecords;

	public override void ExposeData()
	{
		if (Scribe.mode == LoadSaveMode.Saving)
		{
			scribedRecords = new List<CrossSaveCarryPolicyRecord>();
			if (defaultPolicies != null)
			{
				foreach (CarryPolicy policy in defaultPolicies)
				{
					if (policy == null)
					{
						continue;
					}
					CrossSaveCarryPolicyRecord rec = new CrossSaveCarryPolicyRecord
					{
						id = policy.id,
						label = policy.label
					};
					if (policy.entries != null)
					{
						foreach (CarryPolicyEntry entry in policy.entries)
						{
							if (entry == null)
							{
								continue;
							}
							string defName = entry.thing != null ? entry.thing.defName : entry.unloadedDefName;
							if (defName.NullOrEmpty())
							{
								continue;
							}
							rec.entries.Add(new CrossSaveCarryEntryRecord
							{
								thing = defName,
								count = entry.count
							});
						}
					}
					scribedRecords.Add(rec);
				}
			}
			Scribe_Collections.Look(ref scribedRecords, "defaultPolicies", LookMode.Deep);
			scribedRecords = null;
			return;
		}
		if (Scribe.mode == LoadSaveMode.LoadingVars)
		{
			Scribe_Collections.Look(ref scribedRecords, "defaultPolicies", LookMode.Deep);
			return;
		}
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			defaultPolicies = new List<CarryPolicy>();
			if (scribedRecords != null)
			{
				foreach (CrossSaveCarryPolicyRecord rec in scribedRecords)
				{
					if (rec == null)
					{
						continue;
					}
					CarryPolicy policy = new CarryPolicy
					{
						id = rec.id > 0 ? rec.id : NextPolicyId(),
						label = rec.label.NullOrEmpty() ? "UnnamedPolicy".Translate().ToString() : rec.label
					};
					if (rec.entries != null)
					{
						foreach (CrossSaveCarryEntryRecord entryRec in rec.entries)
						{
							if (entryRec == null || entryRec.thing.NullOrEmpty() || entryRec.thing == "null")
							{
								continue;
							}
							ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(entryRec.thing);
							policy.entries.Add(new CarryPolicyEntry
							{
								thing = def,
								unloadedDefName = def == null ? entryRec.thing : null,
								count = entryRec.count
							});
						}
					}
					defaultPolicies.Add(policy);
				}
			}
			scribedRecords = null;
		}
	}

	private int NextPolicyId()
	{
		int maxId = 0;
		if (defaultPolicies != null)
		{
			foreach (CarryPolicy policy in defaultPolicies)
			{
				if (policy != null && policy.id > maxId)
				{
					maxId = policy.id;
				}
			}
		}
		return maxId + 1;
	}

	public CarryPolicy MakeNewPolicy()
	{
		defaultPolicies ??= new List<CarryPolicy>();
		int nextId = NextPolicyId();
		CarryPolicy carryPolicy = new CarryPolicy
		{
			id = nextId,
			label = "CarryPolicy".Translate() + " " + nextId
		};
		defaultPolicies.Add(carryPolicy);
		return carryPolicy;
	}

	public CarryPolicy DefaultCarryPolicy()
	{
		defaultPolicies ??= new List<CarryPolicy>();
		if (defaultPolicies.Count == 0)
		{
			MakeNewPolicy().label = "DefaultCarryPolicy".Translate();
		}
		return defaultPolicies[0];
	}

	public void SetDefault(CarryPolicy policy)
	{
		if (defaultPolicies != null && defaultPolicies.Remove(policy))
		{
			defaultPolicies.Insert(0, policy);
		}
	}

	public AcceptanceReport TryDelete(CarryPolicy policy)
	{
		if (defaultPolicies == null || !defaultPolicies.Remove(policy))
		{
			return false;
		}
		if (defaultPolicies.Count == 0)
		{
			DefaultCarryPolicy();
		}
		return true;
	}
}
