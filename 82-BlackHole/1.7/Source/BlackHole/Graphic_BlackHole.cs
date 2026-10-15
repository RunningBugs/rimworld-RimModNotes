using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BlackHole;

/// <summary>
/// 黑洞专用 Graphic：优先使用 AssetBundle 里的自定义着色器
/// （银河缓转 + GPU 程序化十字星闪烁，_RealTimeSecs 用真实时间，纯 FPS 不受 TPS/暂停影响）；
/// 着色器不可用时自动退回多帧贴图闪烁（Graphic_Twinkle 逻辑）。
/// </summary>
[StaticConstructorOnStartup]
public class Graphic_BlackHole : Graphic_Twinkle
{
    private static bool shaderResolved;
    private static Shader galaxyShader;
    private static Material galaxyMat;
    private static MaterialPropertyBlock propsBlock;

    private bool shaderMode;

    private static Shader GalaxyShader
    {
        get
        {
            if (!shaderResolved)
            {
                shaderResolved = true;
                foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
                {
                    if (mod.PackageIdPlayerFacing != "RunningBugs.BlackHole")
                    {
                        continue;
                    }
                    foreach (AssetBundle bundle in mod.assetBundles.loadedAssetBundles)
                    {
                        foreach (string assetName in bundle.GetAllAssetNames())
                        {
                            if (assetName.ToLowerInvariant().EndsWith(".shader"))
                            {
                                Shader shader = bundle.LoadAsset<Shader>(assetName);
                                if (shader != null && shader.isSupported)
                                {
                                    galaxyShader = shader;
                                    Log.Message("[BlackHole] Custom shader loaded: " + assetName);
                                    return galaxyShader;
                                }
                            }
                        }
                    }
                }
                Log.Message("[BlackHole] Custom shader not found or unsupported, falling back to frame twinkle.");
            }
            return galaxyShader;
        }
    }

    private static Material GalaxyMat
    {
        get
        {
            if (galaxyMat == null && GalaxyShader != null)
            {
                Texture2D mainTex = ContentFinder<Texture2D>.Get("BlackHoleBase/BlackHoleBase", reportFailure: false);
                if (mainTex == null)
                {
                    return null;
                }
                mainTex.wrapMode = TextureWrapMode.Clamp; // 防旋转采样环绕（四角残臂）
                galaxyMat = new Material(GalaxyShader);
                galaxyMat.SetTexture("_MainTex", mainTex);
                galaxyMat.SetFloat("_SpinSpeed", 0.35f);   // ~18秒一圈
                galaxyMat.SetFloat("_StarScale", 1.6f);    // 更大的十字星
                galaxyMat.SetFloat("_StarSpeed", 0.5f);
                galaxyMat.SetFloat("_MaxStars", 4f);
                galaxyMat.SetFloat("_StarSlots", 8f);
            }
            return galaxyMat;
        }
    }

    private static MaterialPropertyBlock PropsBlock => propsBlock ??= new MaterialPropertyBlock();

    public override void Init(GraphicRequest req)
    {
        base.Init(req); // 先加载帧贴图作为后备
        shaderMode = GalaxyMat != null;
    }

    public override Material MatSingle => shaderMode ? GalaxyMat : base.MatSingle;

    public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
    {
        if (!shaderMode)
        {
            base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
            return;
        }
        loc += data.DrawOffsetForRot(rot);
        Quaternion rotation = Quaternion.identity;
        if (extraRotation != 0f)
        {
            rotation = Quaternion.Euler(0f, extraRotation, 0f);
        }
        // 真实时间驱动：纯 FPS 动画，不受游戏速度/暂停影响
        PropsBlock.SetFloat("_RealTimeSecs", Time.realtimeSinceStartup);
        Graphics.DrawMesh(MeshAt(rot), loc, rotation, GalaxyMat, 0, null, 0, PropsBlock);
    }
}
