using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Dialog to handle blueprint import conflicts
public class Dialog_BlueprintImportConflicts : Window
{
    private readonly List<PrefabDef> importingBlueprints;
    private readonly Dictionary<string, PrefabDef> existingBlueprints;
    private readonly Dictionary<string, bool> selectedBlueprints;
    private Vector2 scrollPosition = Vector2.zero;
    private float viewHeight = 0f;
    
    public override Vector2 InitialSize => new Vector2(600f, 500f);

    public Dialog_BlueprintImportConflicts(List<PrefabDef> importingBlueprints, Dictionary<string, PrefabDef> existingBlueprints)
    {
        this.importingBlueprints = importingBlueprints;
        this.existingBlueprints = existingBlueprints;

        selectedBlueprints = new Dictionary<string, bool>();
        foreach (var blueprint in importingBlueprints)
        {
            selectedBlueprints[blueprint.defName] = true; // Default to import all
        }

        doCloseX = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
        resizeable = true;
        draggable = true;
    }
    
    public override void DoWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        
        // Title
        listing.Label("Blueprint2.BlueprintImportConflictsTitle".Translate());
        listing.Gap();
        
        // Description
        listing.Label("Blueprint2.BlueprintImportConflictsDescription".Translate());
        listing.Gap();
        
        // Scroll view for blueprint list - leave space for buttons at bottom
        var remainingHeight = inRect.height - listing.CurHeight - 50f; // 50f for buttons and spacing
        var outRect = listing.GetRect(remainingHeight);
        var viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
        
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        
        var innerListing = new Listing_Standard();
        innerListing.Begin(viewRect);
        
        // Show conflicting blueprints
        foreach (var blueprint in importingBlueprints)
        {
            var hasConflict = existingBlueprints.ContainsKey(blueprint.defName);
            if (hasConflict)
            {
                var rowRect = innerListing.GetRect(30f);
                
                // Checkbox
                var checkRect = new Rect(rowRect.x, rowRect.y, 30f, 30f);
                var selected = selectedBlueprints[blueprint.defName];
                Widgets.Checkbox(checkRect.x, checkRect.y, ref selected);
                selectedBlueprints[blueprint.defName] = selected;
                
                // Blueprint name and conflict warning
                var textRect = new Rect(rowRect.x + 35f, rowRect.y, rowRect.width - 35f, rowRect.height);
                var conflictText = $"{blueprint.label} ({blueprint.defName}) - {"Blueprint2.Conflict".Translate()}";
                Widgets.Label(textRect, conflictText);
                
                innerListing.Gap(2f);
            }
        }
        
        // Show non-conflicting blueprints
        foreach (var blueprint in importingBlueprints)
        {
            var hasConflict = existingBlueprints.ContainsKey(blueprint.defName);
            if (!hasConflict)
            {
                var rowRect = innerListing.GetRect(30f);
                
                // Checkbox (always checked for non-conflicting)
                var checkRect = new Rect(rowRect.x, rowRect.y, 30f, 30f);
                var selected = true;
                Widgets.Checkbox(checkRect.x, checkRect.y, ref selected, disabled: true);
                
                // Blueprint name
                var textRect = new Rect(rowRect.x + 35f, rowRect.y, rowRect.width - 35f, rowRect.height);
                Widgets.Label(textRect, $"{blueprint.label} ({blueprint.defName})");
                
                innerListing.Gap(2f);
            }
        }
        
        viewHeight = innerListing.CurHeight;
        innerListing.End();
        Widgets.EndScrollView();
        
        listing.Gap();
        
        // Buttons
        var buttonRect = listing.GetRect(35f);
        var buttonWidth = 100f;
        var buttonSpacing = 10f;
        
        // Import button on the left
        var importRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
        
        // Cancel button on the right
        var cancelRect = new Rect(buttonRect.xMax - buttonWidth, buttonRect.y, buttonWidth, buttonRect.height);
        
        if (Widgets.ButtonText(importRect, "Blueprint2.ImportSelected".Translate()))
        {
            ImportSelectedBlueprints();
            Close();
        }
        
        if (Widgets.ButtonText(cancelRect, "Blueprint2.Cancel".Translate()))
        {
            Close();
        }
        
        listing.End();
    }
    
    private void ImportSelectedBlueprints()
    {
        var importedCount = 0;
        
        foreach (var blueprint in importingBlueprints)
        {
            // Only import selected blueprints
            if (selectedBlueprints.TryGetValue(blueprint.defName, out bool selected) && selected)
            {
                // Store in unified blueprints for flexible placement
                BlueprintCreateDesignatorBase.savedUnifiedBlueprints[blueprint.defName] = blueprint;
                importedCount++;
            }
        }
        
        Messages.Message("Blueprint2.BlueprintsImportedCount".Translate(importedCount), MessageTypeDefOf.PositiveEvent);
    }
}