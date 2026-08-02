using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RunningBugs.RimLocksmith.Core;
using UnityEngine;
using Verse;

namespace RunningBugs.RimLocksmith;

/// <summary>
/// 门锁 ITab:只读摘要(多门合并视图)+ 操作按钮。
/// 实际编辑在 Dialog_EditLockConfig 弹窗中完成。
/// </summary>
public sealed class ITab_RimLocksmith : ITab
{
    private static readonly Color StateOn = new Color(0.35f, 0.9f, 0.35f);
    private static readonly Color StateOff = new Color(0.95f, 0.4f, 0.35f);
    private static readonly Color StatePartial = new Color(0.95f, 0.85f, 0.4f);
    private static readonly Color TextDim = new Color(0.75f, 0.75f, 0.75f);

    private static LockConfigData clipboard;

    public ITab_RimLocksmith()
    {
        size = new Vector2(480f, 380f);
        labelKey = "RimLocksmith.TabLabel";
    }

    public override bool IsVisible => RimLocksmithUtility.SelectionIsOnlyDoorsWithAtLeastOneColonyDoor();

    private static CompRimLocksmithDoor CompOf(Building_Door door) => door.TryGetComp<CompRimLocksmithDoor>();

    protected override void FillTab()
    {
        List<Building_Door> targets = RimLocksmithUtility.SelectedConfigurableColonyDoors();
        int ignored = RimLocksmithUtility.SelectedDoors().Count - targets.Count;

        Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
        float y = rect.y;
        Text.Font = GameFont.Small;

        if (targets.Count == 0)
        {
            Widgets.Label(new Rect(rect.x, y, rect.width, 26f), "RimLocksmith.NoConfigurableDoors".Translate());
            return;
        }

        bool multi = targets.Count > 1 || ignored > 0;
        GUI.color = TextDim;
        Widgets.Label(new Rect(rect.x, y, rect.width, 24f),
            multi ? "RimLocksmith.CurrentConfigMerged".Translate(targets.Count, ignored) : "RimLocksmith.CurrentConfig".Translate());
        GUI.color = Color.white;
        y += 26f;
        Widgets.DrawLineHorizontal(rect.x, y, rect.width);
        y += 6f;

        DrawSummaryRow(rect.x + 8f, ref y, rect.width - 8f, "RimLocksmith.AllowColonists", MergedBool(targets, c => c.AllowColonists));
        DrawSummaryRow(rect.x + 8f, ref y, rect.width - 8f, "RimLocksmith.AllowSlaves", MergedBool(targets, c => c.AllowSlaves));
        DrawSummaryRow(rect.x + 8f, ref y, rect.width - 8f, "RimLocksmith.AllowGuests", MergedBool(targets, c => c.AllowGuests));
        DrawSummaryRow(rect.x + 8f, ref y, rect.width - 8f, "RimLocksmith.AllowTraders", MergedBool(targets, c => c.AllowTraders));
        DrawSummaryRow(rect.x + 8f, ref y, rect.width - 8f, "RimLocksmith.AnimalAccess",
            MergedMode(targets, c => c.AnimalAccess, UIUtil.ModeLabel, c => c switch { AnimalAccess.All => StateOn, AnimalAccess.None => StateOff, _ => StatePartial }));
        DrawSummaryRow(rect.x + 8f, ref y, rect.width - 8f, "RimLocksmith.MechAccess",
            MergedMode(targets, c => c.MechAccess, UIUtil.ModeLabel, c => c switch { MechAccess.All => StateOn, MechAccess.None => StateOff, _ => StatePartial }));
        y += 2f;
        Widgets.DrawLineHorizontal(rect.x, y, rect.width);
        y += 6f;

        Text.Font = GameFont.Tiny;
        GUI.color = TextDim;
        Widgets.Label(new Rect(rect.x, y, rect.width, 30f), "RimLocksmith.VanillaNote".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        float buttonY = rect.yMax - 30f;
        float gap = 6f;
        float buttonWidth = (rect.width - 3f * gap) / 4f;
        if (Widgets.ButtonText(new Rect(rect.x, buttonY, buttonWidth, 30f), "RimLocksmith.Edit".Translate()))
        {
            Find.WindowStack.Add(new Dialog_EditLockConfig(targets));
        }
        if (Widgets.ButtonText(new Rect(rect.x + (buttonWidth + gap), buttonY, buttonWidth, 30f), "RimLocksmith.Copy".Translate()))
        {
            clipboard = CompOf(targets[0]).Config.Clone();
        }
        bool hasClipboard = clipboard != null;
        if (hasClipboard && Widgets.ButtonText(new Rect(rect.x + 2f * (buttonWidth + gap), buttonY, buttonWidth, 30f), "RimLocksmith.Paste".Translate()))
        {
            foreach (Building_Door door in targets)
            {
                CompOf(door).SetConfig(clipboard, userConfigured: true);
            }
        }
        if (Widgets.ButtonText(new Rect(rect.x + 3f * (buttonWidth + gap), buttonY, buttonWidth, 30f), "RimLocksmith.ResetToDefault".Translate()))
        {
            foreach (Building_Door door in targets)
            {
                CompOf(door).SetConfig(RimLocksmithMod.Settings.DefaultConfig, userConfigured: false);
            }
        }
    }

    private static (string text, Color color) MergedBool(List<Building_Door> doors, Func<LockConfigData, bool> get)
    {
        bool first = get(CompOf(doors[0]).Config);
        if (doors.Any(d => get(CompOf(d).Config) != first))
        {
            return ("RimLocksmith.Mixed".Translate(), StatePartial);
        }
        return first
            ? ("RimLocksmith.State.Allowed".Translate(), StateOn)
            : ("RimLocksmith.State.Denied".Translate(), StateOff);
    }

    private static (string text, Color color) MergedMode<T>(List<Building_Door> doors, Func<LockConfigData, T> get, Func<T, string> modeLabel, Func<T, Color> modeColor) where T : struct, Enum
    {
        T first = get(CompOf(doors[0]).Config);
        if (doors.Any(d => !EqualityComparer<T>.Default.Equals(get(CompOf(d).Config), first)))
        {
            return ("RimLocksmith.Mixed".Translate(), StatePartial);
        }
        return (modeLabel(first), modeColor(first));
    }

    private static void DrawSummaryRow(float x, ref float y, float width, string nameKey, (string text, Color color) state)
    {
        const float rowHeight = 26f;
        Widgets.Label(new Rect(x, y, width * 0.55f, rowHeight), nameKey.Translate());
        GUI.color = state.color;
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(new Rect(x + width * 0.55f, y, width * 0.45f, rowHeight), state.text);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        y += rowHeight;
    }
}
