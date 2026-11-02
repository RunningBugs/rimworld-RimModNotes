using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using Steamworks;

if (args.Length != 2) throw new ArgumentException("Expected MOD directory and game directory.");
var mod = Path.GetFullPath(args[0]);
var game = Path.GetFullPath(args[1]);
var content = Path.Combine(mod, ".build/publish/content");
var about = XDocument.Load(Path.Combine(content, "About/About.xml")).Root!;
if (about.Element("packageId")?.Value != "RunningBugs.CultistKidnapping")
    throw new InvalidOperationException("Unexpected mod package id.");
var idFile = Path.Combine(mod, "About/PublishedFileId.txt");
var stagedIdFile = Path.Combine(content, "About/PublishedFileId.txt");
var app = new AppId_t(294100);
NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, (name, assembly, searchPath) =>
    name.Contains("steam_api", StringComparison.OrdinalIgnoreCase)
        ? NativeLibrary.Load(Path.Combine(game, "libsteam_api.so")) : IntPtr.Zero);
Environment.SetEnvironmentVariable("SteamAppId", "294100");
File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "steam_appid.txt"), "294100");
if (!SteamAPI.Init()) throw new InvalidOperationException("Steam initialization failed. Steam must be running and logged in.");
try
{
    if (SteamUtils.GetAppID() != app || !SteamUser.BLoggedOn())
        throw new InvalidOperationException("Expected a logged-in RimWorld Steam session.");
    var id = File.Exists(idFile) ? new PublishedFileId_t(ulong.Parse(File.ReadAllText(idFile).Trim())) : PublishedFileId_t.Invalid;
    if (id == PublishedFileId_t.Invalid)
    {
        var created = Wait<CreateItemResult_t>(SteamUGC.CreateItem(app, EWorkshopFileType.k_EWorkshopFileTypeFirst));
        Check(created.m_eResult, "CreateItem");
        id = created.m_nPublishedFileId;
        // Persist immediately: a later retry updates this item instead of creating another.
        File.WriteAllText(idFile, id.m_PublishedFileId + "\n");
        Console.WriteLine("Created item " + id);
        if (created.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            Console.WriteLine("Steam reports that the owner must accept the Workshop legal agreement.");
    }
    else
    {
        var existing = Wait<SteamUGCRequestUGCDetailsResult_t>(SteamUGC.RequestUGCDetails(id, 0));
        Check(existing.m_details.m_eResult, "Owner check");
        if (existing.m_details.m_ulSteamIDOwner != SteamUser.GetSteamID().m_SteamID || existing.m_details.m_nConsumerAppID != app)
            throw new InvalidOperationException("Existing item is not owned by this account for RimWorld.");
    }
    File.Copy(idFile, stagedIdFile, overwrite: true);
    var handle = SteamUGC.StartItemUpdate(app, id);
    Require(SteamUGC.SetItemTitle(handle, about.Element("name")!.Value), "title");
    Require(SteamUGC.SetItemDescription(handle, about.Element("description")!.Value), "description");
    Require(SteamUGC.SetItemPreview(handle, Path.Combine(content, "About/Preview.png")), "preview");
    var tags = new List<string> { "Mod" };
    tags.AddRange(about.Element("supportedVersions")!.Elements("li").Select(node => node.Value));
    Require(SteamUGC.SetItemTags(handle, tags), "tags");
    Require(SteamUGC.SetItemVisibility(handle, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic), "visibility");
    Require(SteamUGC.SetItemContent(handle, content), "content");
    var uploaded = Wait<SubmitItemUpdateResult_t>(SteamUGC.SubmitItemUpdate(handle,
        "Cultist Kidnapping 1.0.0: escorted abduction, carrier replacement and configurable follow-up raids. RimWorld 1.5/1.6/1.7."));
    Check(uploaded.m_eResult, "SubmitItemUpdate");
    var details = Wait<SteamUGCRequestUGCDetailsResult_t>(SteamUGC.RequestUGCDetails(id, 0));
    Check(details.m_details.m_eResult, "Read back uploaded item");
    var result = new { published_file_id = id.m_PublishedFileId,
        url = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id.m_PublishedFileId,
        result = uploaded.m_eResult.ToString(), needs_legal_agreement = uploaded.m_bUserNeedsToAcceptWorkshopLegalAgreement,
        title = details.m_details.m_rgchTitle, visibility = details.m_details.m_eVisibility.ToString(),
        bytes = details.m_details.m_nFileSize, banned = details.m_details.m_bBanned };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(mod, ".build/publish/result.json"), json + "\n");
    Console.WriteLine(json);
}
finally { SteamAPI.Shutdown(); }

static void Require(bool success, string field)
{
    if (!success) throw new InvalidOperationException("Steam rejected item " + field + ".");
}
static void Check(EResult result, string operation)
{
    if (result != EResult.k_EResultOK) throw new InvalidOperationException(operation + " failed: " + result);
}
static T Wait<T>(SteamAPICall_t call) where T : struct
{
    var done = false;
    var failed = false;
    T value = default;
    using var callback = CallResult<T>.Create((result, ioFailure) => { value = result; failed = ioFailure; done = true; });
    callback.Set(call);
    var timer = Stopwatch.StartNew();
    var reported = 0;
    while (!done)
    {
        SteamAPI.RunCallbacks();
        if (timer.Elapsed.TotalSeconds > 180) throw new TimeoutException("Steam callback timed out: " + typeof(T).Name);
        if (timer.Elapsed.Seconds / 10 > reported)
        {
            reported = timer.Elapsed.Seconds / 10;
            Console.WriteLine("Waiting for " + typeof(T).Name + "...");
        }
        Thread.Sleep(50);
    }
    if (failed) throw new IOException("Steam reported an I/O failure: " + typeof(T).Name);
    return value;
}
