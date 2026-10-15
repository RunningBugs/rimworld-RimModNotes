using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Text;
using System.IO;

namespace Blueprint2;

// Clipboard utility for import/export
public static class BlueprintClipboard
{
    public static string ExportPrefabToXml(PrefabDef prefab)
    {
        return BlueprintXmlSerializer.SerializeBlueprint(prefab);
    }

    public static void ExportToClipboard(PrefabDef prefab)
    {
        try
        {
            var xml = ExportPrefabToXml(prefab);
            if (xml != null)
            {
                GUIUtility.systemCopyBuffer = xml;
                Messages.Message("Blueprint2.BlueprintExportedToClipboard".Translate(prefab.label), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("Blueprint2.FailedToExportBlueprint".Translate(), MessageTypeDefOf.RejectInput);
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"Failed to copy blueprint to clipboard: {ex.Message}");
            Messages.Message("Blueprint2.FailedToExportBlueprint".Translate(), MessageTypeDefOf.RejectInput);
        }
    }

    public static PrefabDef ImportFromClipboard()
    {
        try
        {
            var xml = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(xml))
            {
                // Don't show message here, let the caller handle it
                return null;
            }

            var result = BlueprintXmlSerializer.DeserializeBlueprint(xml);
            if (result == null)
            {
                // Don't show message here, let the catch block handle it
                throw new System.Exception("Deserialization returned null");
            }
            return result;
        }
        catch (System.Exception ex)
        {
            Log.Warning($"Failed to import blueprint from clipboard: {ex.Message}");
            // Don't show message here, let the caller handle it
            return null;
        }
    }
    
    public static void ExportAllBlueprintsToClipboard()
    {
        try
        {
            // Collect all blueprints from all storage dictionaries
            var allBlueprints = new List<PrefabDef>();
            
            // Add unified blueprints
            allBlueprints.AddRange(BlueprintCreateDesignatorBase.savedUnifiedBlueprints.Values);
            
            // Add building blueprints
            allBlueprints.AddRange(BlueprintCreateDesignatorBase.savedBuildingBlueprints.Values);
            
            // Add terrain blueprints
            allBlueprints.AddRange(BlueprintCreateDesignatorBase.savedTerrainBlueprints.Values);
            
            if (allBlueprints.Count == 0)
            {
                Messages.Message("Blueprint2.NoBlueprintsToExport".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            
            var xml = BlueprintXmlSerializer.SerializeBlueprints(allBlueprints);
            if (xml != null)
            {
                GUIUtility.systemCopyBuffer = xml;
                Messages.Message("Blueprint2.BlueprintsExportedToClipboard".Translate(allBlueprints.Count), MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("Blueprint2.FailedToExportBlueprints".Translate(), MessageTypeDefOf.RejectInput);
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"Failed to export all blueprints to clipboard: {ex.Message}");
            Messages.Message("Failed to export blueprints", MessageTypeDefOf.RejectInput);
        }
    }
    
    public static List<PrefabDef> ImportAllBlueprintsFromClipboard()
    {
        try
        {
            var xml = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(xml))
            {
                // Don't show message here, let the caller handle it
                return null;
            }

            var prefabs = BlueprintXmlSerializer.DeserializeBlueprints(xml);
            if (prefabs != null && prefabs.Count > 0)
            {
                Messages.Message("BlueprintsImportedFromClipboard".Translate(prefabs.Count), MessageTypeDefOf.PositiveEvent);
                return prefabs;
            }
            else
            {
                // Don't show message here, let the catch block handle it
                throw new System.Exception("Deserialization returned null or empty list");
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"Failed to import blueprints from clipboard: {ex.Message}");
            // Don't show message here, let the caller handle it
            return null;
        }
    }
}