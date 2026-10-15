using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
namespace RW17.Validation;
public static class Mod12Checks
{
    public const string PackageId = "RunningBugs.RimSavesMoreAutosaveSlotsPatch";
    public static void Run(Map map)
    {
        var saveRoot = Path.GetFullPath(GenFilePaths.SaveDataFolderPath);
        if (!saveRoot.StartsWith("/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/", StringComparison.Ordinal)) throw new Exception("Mod12: refusing files outside isolated case");
        Type TypeIn(string assembly, string type) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == assembly).GetType(type, true);
        var rimSettings = TypeIn("RimSaves", "aRandomKiwi.ARS.Settings");
        var folder = rimSettings.GetField("curFolder");
        var masSettings = TypeIn("Revolus.MoreAutosaveSlots", "Revolus.MoreAutosaveSlots.MoreAutosaveSlotsSettings");
        var settings = TypeIn("Revolus.MoreAutosaveSlots", "Revolus.MoreAutosaveSlots.MoreAutosaveSlotsMod").GetField("Settings").GetValue(null);
        var count = AccessTools.Field(masSettings, "count");
        var format = AccessTools.Field(masSettings, "format");
        var next = masSettings.GetMethod("NextName");
        if (Harmony.GetPatchInfo(next)?.Prefixes.Any(p => p.PatchMethod.DeclaringType?.FullName == "RimSavesMASPatch.Patches") != true) throw new Exception("Mod12: concrete NextName patch absent");
        var oldFolder = folder.GetValue(null); var oldCount = count.GetValue(settings); var oldFormat = format.GetValue(settings);
        var stem = "RW17T12" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var created = new List<string>();
        string Name(int slot) => stem + "-" + slot;
        string PathFor(string prefix, int slot) => GenFilePaths.FilePathForSavedGame(prefix + Name(slot));
        void Touch(string prefix, int slot, DateTime stamp) { var path = PathFor(prefix, slot); Directory.CreateDirectory(Path.GetDirectoryName(path)); if (File.Exists(path)) throw new Exception("Mod12: fixture name collision"); File.WriteAllText(path, "isolated slot fixture; not a loadable save"); created.Add(path); File.SetLastWriteTimeUtc(path, stamp); }
        string Next() => (string)next.Invoke(null, null);
        try
        {
            count.SetValue(settings, 3); format.SetValue(settings, stem + "-{index}"); folder.SetValue(null, "Default");
            if (Next() != Name(1)) throw new Exception("Mod12: empty Default did not choose first slot");
            var a = stem + "A"; var b = stem + "B"; var now = DateTime.UtcNow;
            Touch(a + "#§#", 1, now.AddHours(-1));
            folder.SetValue(null, a);
            if (Next() != Name(2)) throw new Exception("Mod12: existing folder-local slot did not select next empty slot");
            folder.SetValue(null, b);
            if (Next() != Name(1)) throw new Exception("Mod12: other folder leaked occupancy");
            folder.SetValue(null, a); Touch(a + "#§#", 2, now.AddHours(-3)); Touch(a + "#§#", 3, now.AddHours(-2));
            if (Next() != Name(2)) throw new Exception("Mod12: full folder did not select oldest real file");
            folder.SetValue(null, "Default");
            if (Next() != Name(1)) throw new Exception("Mod12: virtual-folder files leaked into Default");
        }
        finally
        {
            folder.SetValue(null, oldFolder); count.SetValue(settings, oldCount); format.SetValue(settings, oldFormat);
            foreach (var path in created) File.Delete(path);
        }
        Console.WriteLine("RW17_MOD12_REAL_FOLDER_SLOT_SELECTION_OK; complete autosave/reload and upstream prefix ordering remain pending");
    }
}
