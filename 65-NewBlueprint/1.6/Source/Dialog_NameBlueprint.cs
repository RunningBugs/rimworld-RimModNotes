using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;

namespace Blueprint2;

// Dialog for naming blueprints
public class Dialog_NameBlueprint : Window
{
    private string blueprintName;
    private readonly System.Action<string> onConfirm;
    
    public override Vector2 InitialSize => new Vector2(400f, 200f);

    public Dialog_NameBlueprint(string defaultName, System.Action<string> onConfirm)
    {
        this.blueprintName = defaultName;
        this.onConfirm = onConfirm;
        this.forcePause = true;
        this.doCloseX = true;
        this.closeOnAccept = false;
        this.closeOnCancel = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        
        listing.Label("Blueprint2.EnterBlueprintName".Translate());
        listing.Gap(12f);
        
        // Name input field
        blueprintName = listing.TextEntry(blueprintName);
        
        listing.Gap(12f);
        
        // Buttons
        var buttonRect = listing.GetRect(30f);
        var buttonWidth = (buttonRect.width - 10f) / 2f;
        
        if (Widgets.ButtonText(new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height), "Blueprint2.Create".Translate()))
        {
            if (!string.IsNullOrWhiteSpace(blueprintName))
            {
                onConfirm?.Invoke(blueprintName.Trim());
                Close();
            }
        }
        
        if (Widgets.ButtonText(new Rect(buttonRect.x + buttonWidth + 10f, buttonRect.y, buttonWidth, buttonRect.height), "Blueprint2.Cancel".Translate()))
        {
            Close();
        }
        
        listing.End();
        
        // Handle Enter key
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            if (!string.IsNullOrWhiteSpace(blueprintName))
            {
                onConfirm?.Invoke(blueprintName.Trim());
                Close();
            }
            Event.current.Use();
        }
    }

    public override void OnAcceptKeyPressed()
    {
        if (!string.IsNullOrWhiteSpace(blueprintName))
        {
            onConfirm?.Invoke(blueprintName.Trim());
            Close();
        }
    }
}