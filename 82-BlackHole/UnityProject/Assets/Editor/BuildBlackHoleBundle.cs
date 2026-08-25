using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

public static class BuildBlackHoleBundle
{
    const string OutputDir = "AssetBundleBuild";
    const string ModAssetBundles = "../AssetBundles";

    [MenuItem("BlackHole/Build All Bundles")]
    public static void Build()
    {
        Directory.CreateDirectory(OutputDir);
        bool allOk = true;

        // Linux：RimWorld Linux 跑 OpenGL 4.5
        allOk &= BuildOne(BuildTarget.StandaloneLinux64, "blackhole_linux", GraphicsDeviceType.OpenGLCore, GraphicsDeviceType.Vulkan);
        // Windows：RimWorld Windows 跑 D3D11
        allOk &= BuildOne(BuildTarget.StandaloneWindows64, "blackhole_win", GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore);
        // macOS：RimWorld Mac 跑 Metal
        allOk &= BuildOne(BuildTarget.StandaloneOSX, "blackhole_mac", GraphicsDeviceType.Metal);

        if (!allOk)
        {
            Debug.LogError("[BlackHole] One or more bundle builds failed.");
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log("[BlackHole] All bundles built OK.");
    }

    public static void BuildBlackHole_LinuxWin()
    {
        Directory.CreateDirectory(OutputDir);
        bool ok = BuildOne(BuildTarget.StandaloneLinux64, "blackhole_linux", GraphicsDeviceType.OpenGLCore, GraphicsDeviceType.Vulkan);
        ok &= BuildOne(BuildTarget.StandaloneWindows64, "blackhole_win", GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLCore);
        if (!ok) EditorApplication.Exit(1);
    }

    static bool BuildOne(BuildTarget target, string outputName, params GraphicsDeviceType[] apis)
    {
        var originalTarget = EditorUserBuildSettings.activeBuildTarget;
        PlayerSettings.SetGraphicsAPIs(target, apis);
        if (target != originalTarget)
        {
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
            if (!switched)
            {
                Debug.LogError("[BlackHole] Failed to switch build target to " + target);
                return false;
            }
        }
        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = "blackhole",
                assetNames = new[] { "Assets/Shaders/BlackHoleGalaxy.shader" }
            }
        };
        var manifest = BuildPipeline.BuildAssetBundles(
            OutputDir,
            builds,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            target);
        if (manifest == null)
        {
            Debug.LogError("[BlackHole] BuildAssetBundles failed for " + target);
            return false;
        }
        string src = Path.Combine(OutputDir, "blackhole");
        if (!File.Exists(src))
        {
            Debug.LogError("[BlackHole] bundle not found: " + src);
            return false;
        }
        Directory.CreateDirectory(ModAssetBundles);
        string dst = Path.Combine(ModAssetBundles, outputName);
        if (File.Exists(dst)) File.Delete(dst);
        File.Copy(src, dst);
        Debug.Log("[BlackHole] OK " + target + " -> " + Path.GetFullPath(dst) + " (APIs: " + string.Join(",", apis) + ")");
        return true;
    }
}
