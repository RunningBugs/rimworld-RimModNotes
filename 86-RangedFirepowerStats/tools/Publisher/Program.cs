using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Steamworks;

if (args.Length < 2 || args.Skip(2).Any(a => a is not "--check" and not "--verify"))
    throw new ArgumentException("Expected project directory, game directory, and optional --check or --verify.");
var project = Path.GetFullPath(args[0]);
var game = Path.GetFullPath(args[1]);
var content = Path.Combine(project, "dist/RangedFirepowerStats");
var output = Path.Combine(project, ".build/publish");
Directory.CreateDirectory(output);
const string packageId = "RunningBugs.RangedFirepowerStats";
var about = XDocument.Load(Path.Combine(content, "About/About.xml")).Root!;
if (about.Element("packageId")?.Value != packageId) throw new InvalidDataException("Unexpected package id.");
var title = about.Element("name")!.Value;
var description = about.Element("description")!.Value;
if (Encoding.UTF8.GetByteCount(title) > 128) throw new InvalidDataException("Workshop title is too long.");
var preview = Path.Combine(content, "About/Preview.png");
var gallery = Directory.GetFiles(Path.Combine(content, "About"), "*.jpg").Order(StringComparer.Ordinal).ToArray();
if (!File.Exists(preview) || new FileInfo(preview).Length >= 1_000_000 || gallery.Length != 6 || gallery.Any(p => new FileInfo(p).Length >= 1_000_000))
    throw new InvalidDataException("Expected the reviewed cover and six gallery images, each under 1 MB.");
var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "docs/workshop-package.json")));
foreach (var item in manifest.RootElement.GetProperty("files").EnumerateArray())
{
    var path = Path.GetFullPath(Path.Combine(content, item.GetProperty("path").GetString()!));
    if (!path.StartsWith(content + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("Path outside content.");
    if (!File.Exists(path) || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != item.GetProperty("sha256").GetString())
        throw new InvalidDataException("Staging manifest mismatch: " + path);
}
var forbidden = new HashSet<string>([".build", ".venv", "Source", "Tests", "obj", "bin"], StringComparer.OrdinalIgnoreCase);
foreach (var path in Directory.EnumerateFileSystemEntries(content, "*", SearchOption.AllDirectories))
    if (forbidden.Contains(Path.GetFileName(path)) || new FileInfo(path).LinkTarget != null)
        throw new InvalidDataException("Unexpected development file or symlink: " + path);

var idFile = Path.Combine(project, "About/PublishedFileId.txt");
var stagedIdFile = Path.Combine(content, "About/PublishedFileId.txt");
var app = new AppId_t(294100);
var harmony = new PublishedFileId_t(2009463077);
NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, (name, assembly, searchPath) =>
    name.Contains("steam_api", StringComparison.OrdinalIgnoreCase)
        ? NativeLibrary.Load(Path.Combine(game, "libsteam_api.so")) : IntPtr.Zero);
Environment.SetEnvironmentVariable("SteamAppId", "294100");
File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "steam_appid.txt"), "294100");
if (!SteamAPI.Init()) throw new InvalidOperationException("Steam initialization failed; the logged-in Steam client must be available.");
try
{
    if (SteamUtils.GetAppID() != app || !SteamUser.BLoggedOn()) throw new InvalidOperationException("Expected a logged-in RimWorld Steam session.");
    var id = File.Exists(idFile) ? new PublishedFileId_t(ulong.Parse(File.ReadAllText(idFile).Trim())) : PublishedFileId_t.Invalid;
    if (File.Exists(stagedIdFile) && id != PublishedFileId_t.Invalid && File.ReadAllText(stagedIdFile).Trim() != id.ToString())
        throw new InvalidDataException("Source and staged publication IDs differ.");
    if (args.Contains("--check"))
    {
        if (id != PublishedFileId_t.Invalid) CheckOwner(Query(id, app), app);
        Console.WriteLine(JsonSerializer.Serialize(new { checked_ok = true, logged_in = true, app_id = app.m_AppId,
            package_id = packageId, title, content, create_new = id == PublishedFileId_t.Invalid, gallery_images = gallery.Length,
            files = Directory.GetFiles(content, "*", SearchOption.AllDirectories).Length }, JsonOptions()));
        return;
    }
    if (args.Contains("--verify"))
    {
        if (id == PublishedFileId_t.Invalid) throw new InvalidOperationException("No published item ID to verify.");
        var snapshot = Query(id, app);
        CheckOwner(snapshot, app);
        Verify(snapshot, title, description, packageId, gallery, harmony);
        SaveResult(snapshot, project, false);
        return;
    }

    if (id == PublishedFileId_t.Invalid)
    {
        Console.WriteLine("Creating a new RimWorld Workshop item...");
        var created = Wait<CreateItemResult_t>(SteamUGC.CreateItem(app, EWorkshopFileType.k_EWorkshopFileTypeFirst));
        Check(created.m_eResult, "CreateItem");
        id = created.m_nPublishedFileId;
        // Store immediately so a retry can never create a duplicate item.
        File.WriteAllText(idFile, id.m_PublishedFileId + "\n");
        File.WriteAllText(stagedIdFile, id.m_PublishedFileId + "\n");
        Console.WriteLine("Created item " + id.m_PublishedFileId);
        if (created.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            Console.WriteLine("Steam reports a Workshop legal agreement requiring the owner's acceptance.");
    }
    var before = Query(id, app);
    CheckOwner(before, app);
    if (!string.IsNullOrWhiteSpace(before.Metadata))
    {
        var remote = JsonDocument.Parse(before.Metadata);
        if (remote.RootElement.TryGetProperty("packageId", out var remotePackage) && remotePackage.GetString() != packageId)
            throw new InvalidOperationException("Existing item belongs to a different mod package.");
    }
    File.Copy(idFile, stagedIdFile, true);
    var handle = SteamUGC.StartItemUpdate(app, id);
    Require(SteamUGC.SetItemUpdateLanguage(handle, "english"), "language");
    Require(SteamUGC.SetItemTitle(handle, title), "title");
    Require(SteamUGC.SetItemDescription(handle, description), "description");
    Require(SteamUGC.SetItemPreview(handle, preview), "cover");
    var tags = new List<string> { "Mod" };
    tags.AddRange(about.Element("supportedVersions")!.Elements("li").Select(node => node.Value));
    Require(SteamUGC.SetItemTags(handle, tags), "tags");
    Require(SteamUGC.SetItemMetadata(handle, JsonSerializer.Serialize(new { packageId, version = "1.0.0" })), "metadata");
    Require(SteamUGC.SetItemVisibility(handle, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic), "public visibility");
    Require(SteamUGC.SetItemContent(handle, content), "content");
    foreach (var image in gallery)
    {
        var existing = before.Previews.FirstOrDefault(p => Path.GetFileName(p.FileName) == Path.GetFileName(image));
        if (existing != null) Require(SteamUGC.UpdateItemPreviewFile(handle, existing.Index, image), "gallery update " + Path.GetFileName(image));
        else Require(SteamUGC.AddItemPreviewFile(handle, image, EItemPreviewType.k_EItemPreviewType_Image), "gallery image " + Path.GetFileName(image));
    }
    Console.WriteLine("Uploading runtime content, cover and six gallery images...");
    var uploaded = Wait<SubmitItemUpdateResult_t>(SteamUGC.SubmitItemUpdate(handle,
        "Ranged Firepower Stats 1.0.0: read-only ranged DPS, accuracy profiles, per-cell tables and charts. RimWorld 1.7."), () =>
        {
            var state = SteamUGC.GetItemUpdateProgress(handle, out var done, out var total);
            Console.WriteLine($"Upload state: {state}; {done}/{total} bytes.");
        });
    Check(uploaded.m_eResult, "SubmitItemUpdate");
    if (!before.Children.Contains(harmony.m_PublishedFileId))
    {
        var dependency = Wait<AddUGCDependencyResult_t>(SteamUGC.AddDependency(id, harmony));
        Check(dependency.m_eResult, "Harmony dependency");
    }
    Snapshot? after = null;
    Exception? pending = null;
    for (int attempt = 0; attempt < 12; attempt++)
    {
        after = Query(id, app);
        CheckOwner(after, app);
        try { Verify(after, title, description, packageId, gallery, harmony); pending = null; break; }
        catch (InvalidDataException e) { pending = e; Thread.Sleep(2500); }
    }
    if (pending != null) throw pending;
    SaveResult(after!, project, uploaded.m_bUserNeedsToAcceptWorkshopLegalAgreement);
}
finally { SteamAPI.Shutdown(); }

static void Verify(Snapshot data, string title, string description, string packageId, string[] gallery, PublishedFileId_t harmony)
{
    if (data.Title != title || data.Description != description) throw new InvalidDataException("Remote title/description has not matched the uploaded metadata.");
    if (data.Visibility != ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic || data.Banned || data.FileBytes == 0)
        throw new InvalidDataException("Remote item is not a public item with content.");
    if (string.IsNullOrWhiteSpace(data.PreviewUrl)) throw new InvalidDataException("Primary preview is missing.");
    if (!gallery.All(p => data.Previews.Any(remote => Path.GetFileName(remote.FileName) == Path.GetFileName(p))))
        throw new InvalidDataException("Remote gallery is incomplete.");
    if (!data.Children.Contains(harmony.m_PublishedFileId)) throw new InvalidDataException("Remote Harmony requirement is missing.");
    using var metadata = JsonDocument.Parse(data.Metadata);
    if (metadata.RootElement.GetProperty("packageId").GetString() != packageId) throw new InvalidDataException("Remote package metadata differs.");
}

static void CheckOwner(Snapshot data, AppId_t app)
{
    if (data.Owner != SteamUser.GetSteamID().m_SteamID || data.AppId != app.m_AppId)
        throw new InvalidOperationException("Workshop item is not owned by the logged-in user for RimWorld.");
}

static Snapshot Query(PublishedFileId_t id, AppId_t app)
{
    var handle = SteamUGC.CreateQueryUGCDetailsRequest([id], 1);
    try
    {
        Require(SteamUGC.SetAllowCachedResponse(handle, 0), "fresh query");
        Require(SteamUGC.SetLanguage(handle, "english"), "query language");
        Require(SteamUGC.SetReturnLongDescription(handle, true), "query description");
        Require(SteamUGC.SetReturnAdditionalPreviews(handle, true), "query gallery");
        Require(SteamUGC.SetReturnChildren(handle, true), "query dependencies");
        Require(SteamUGC.SetReturnMetadata(handle, true), "query metadata");
        var result = Wait<SteamUGCQueryCompleted_t>(SteamUGC.SendQueryUGCRequest(handle));
        Check(result.m_eResult, "Query item");
        Require(SteamUGC.GetQueryUGCResult(handle, 0, out var details), "query result");
        Check(details.m_eResult, "Query item details");
        var images = new List<Preview>();
        uint count = SteamUGC.GetQueryUGCNumAdditionalPreviews(handle, 0);
        for (uint index = 0; index < count; index++)
        {
            Require(SteamUGC.GetQueryUGCAdditionalPreview(handle, 0, index, out var url, 4096, out var name, 1024, out var type), "query image");
            images.Add(new Preview(index, name, url, type.ToString()));
        }
        var children = new PublishedFileId_t[details.m_unNumChildren];
        if (children.Length > 0) Require(SteamUGC.GetQueryUGCChildren(handle, 0, children, (uint)children.Length), "query required items");
        SteamUGC.GetQueryUGCPreviewURL(handle, 0, out var previewUrl, 4096);
        SteamUGC.GetQueryUGCMetadata(handle, 0, out var metadata, 5000);
        return new Snapshot(id.m_PublishedFileId, details.m_ulSteamIDOwner, details.m_nConsumerAppID.m_AppId,
            details.m_rgchTitle, details.m_rgchDescription, details.m_eVisibility, details.m_nFileSize,
            details.m_bBanned, previewUrl, metadata, images, children.Select(child => child.m_PublishedFileId).ToArray());
    }
    finally { SteamUGC.ReleaseQueryUGCRequest(handle); }
}

static void SaveResult(Snapshot data, string project, bool needsLegalAgreement)
{
    var record = new { published_file_id = data.Id, url = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + data.Id,
        result = "OK", needs_legal_agreement = needsLegalAgreement, title = data.Title,
        visibility = data.Visibility.ToString(), bytes = data.FileBytes, banned = data.Banned,
        preview_url = data.PreviewUrl, gallery = data.Previews, required_items = data.Children, verified_utc = DateTime.UtcNow };
    var json = JsonSerializer.Serialize(record, JsonOptions());
    File.WriteAllText(Path.Combine(project, ".build/publish/result.json"), json + "\n");
    File.WriteAllText(Path.Combine(project, "docs/published.json"), json + "\n");
    Console.WriteLine(json);
}

static void Require(bool success, string field)
{
    if (!success) throw new InvalidOperationException("Steam rejected " + field + ".");
}
static void Check(EResult result, string operation)
{
    if (result != EResult.k_EResultOK) throw new InvalidOperationException(operation + " failed: " + result);
}
static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };
static T Wait<T>(SteamAPICall_t call, Action? progress = null) where T : struct
{
    bool done = false, failed = false;
    T value = default;
    using var callback = CallResult<T>.Create((result, ioFailure) => { value = result; failed = ioFailure; done = true; });
    callback.Set(call);
    var timer = Stopwatch.StartNew();
    var nextReport = TimeSpan.FromSeconds(10);
    while (!done)
    {
        SteamAPI.RunCallbacks();
        if (timer.Elapsed.TotalSeconds > 180) throw new TimeoutException("Steam callback timed out: " + typeof(T).Name);
        if (timer.Elapsed >= nextReport)
        {
            Console.WriteLine("Waiting for " + typeof(T).Name + "...");
            progress?.Invoke();
            nextReport += TimeSpan.FromSeconds(10);
        }
        Thread.Sleep(50);
    }
    if (failed) throw new IOException("Steam I/O failure: " + typeof(T).Name);
    return value;
}

sealed record Preview(uint Index, string FileName, string Url, string Type);
sealed record Snapshot(ulong Id, ulong Owner, uint AppId, string Title, string Description,
    ERemoteStoragePublishedFileVisibility Visibility, int FileBytes, bool Banned, string PreviewUrl,
    string Metadata, List<Preview> Previews, ulong[] Children);
