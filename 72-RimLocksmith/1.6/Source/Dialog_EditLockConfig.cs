using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RunningBugs.RimLocksmith.Core;
using UnityEngine;
using Verse;

namespace RunningBugs.RimLocksmith;

/// <summary>
/// 编辑选中门锁配置的弹窗:4 个复选框 + 2 个三态档位。
/// 多门时混合项标注"点击统一",修改应用到所有选中的可配置殖民地门。
/// </summary>
public sealed class Dialog_EditLockConfig : Window
{
    private readonly List<Building_Door> doors;

    public Dialog_EditLockConfig(List<Building_Door> doors)
    {
        this.doors = doors;
        doCloseX = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new Vector2(420f, 340f);

    private static CompRimLocksmithDoor CompOf(Building_Door door) => door.TryGetComp<CompRimLocksmithDoor>();

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f),
            "RimLocksmith.EditDialogTitle".Translate(doors.Count));
        float y = inRect.y + 36f;
        Widgets.DrawLineHorizontal(inRect.x, y, inRect.width);
        y += 8f;

        Listing_Standard listing = new Listing_Standard();
        listing.Begin(new Rect(inRect.x, y, inRect.width, inRect.height - y));
        DrawBulkToggle(listing, "RimLocksmith.AllowColonists", c => c.AllowColonists, (c, v) => c.AllowColonists = v);
        DrawBulkToggle(listing, "RimLocksmith.AllowSlaves", c => c.AllowSlaves, (c, v) => c.AllowSlaves = v);
        DrawBulkToggle(listing, "RimLocksmith.AllowGuests", c => c.AllowGuests, (c, v) => c.AllowGuests = v);
        DrawBulkToggle(listing, "RimLocksmith.AllowTraders", c => c.AllowTraders, (c, v) => c.AllowTraders = v);
        DrawBulkMode(listing, "RimLocksmith.AnimalAccess", c => c.AnimalAccess, (c, v) => c.AnimalAccess = v, UIUtil.ModeLabel);
        DrawBulkMode(listing, "RimLocksmith.MechAccess", c => c.MechAccess, (c, v) => c.MechAccess = v, UIUtil.ModeLabel);
        listing.End();

        Text.Font = GameFont.Tiny;
        GUI.color = new Color(0.75f, 0.75f, 0.75f);
        Widgets.Label(new Rect(inRect.x, inRect.yMax - 30f, inRect.width, 30f),
            "RimLocksmith.VanillaNote".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private void Apply(System.Action<LockConfigData> edit)
    {
        foreach (Building_Door door in doors)
        {
            CompRimLocksmithDoor comp = CompOf(door);
            LockConfigData cfg = comp.Config;
            edit(cfg);
            cfg.UserConfigured = true;
            comp.NotifyChanged();
        }
    }

    private void DrawBulkToggle(Listing_Standard listing, string key, System.Func<LockConfigData, bool> get, System.Action<LockConfigData, bool> set)
    {
        bool first = get(CompOf(doors[0]).Config);
        bool mixed = doors.Any(d => get(CompOf(d).Config) != first);
        string label = key.Translate().ToString() + (mixed ? " (" + "RimLocksmith.MixedClickToUnify".Translate().ToString() + ")" : string.Empty);
        bool value = !mixed && first;
        bool before = value;
        listing.CheckboxLabeled(label, ref value, "RimLocksmith.AllowOpenTooltip".Translate());
        if (value != before || mixed && value)
        {
            Apply(c => set(c, value));
        }
    }

    private void DrawBulkMode<T>(Listing_Standard listing, string key, System.Func<LockConfigData, T> get, System.Action<LockConfigData, T> set, System.Func<T, string> modeLabel) where T : struct, System.Enum
    {
        T first = get(CompOf(doors[0]).Config);
        bool mixed = doors.Any(d => !EqualityComparer<T>.Default.Equals(get(CompOf(d).Config), first));
        string label = key.Translate().ToString();
        Rect row = listing.GetRect(26f);
        Widgets.Label(row.LeftHalf(), label);
        if (Widgets.ButtonText(row.RightHalf().ContractedBy(0f, 1f), mixed ? "RimLocksmith.Mixed".Translate().ToString() : modeLabel(first)))
        {
            UIUtil.OpenModeMenu<T>(mode => Apply(c => set(c, mode)), modeLabel);
        }
        listing.Gap(2f);
    }
}
