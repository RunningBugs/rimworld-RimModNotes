using UnityEngine;
using Verse;

namespace BlackHole;

/// <summary>
/// 多帧闪烁贴图：每隔 TicksPerFrame 切换一帧（不同建筑相位错开），
/// 用于黑洞星系上"一闪一闪"的白色星点。帧贴图放在同一目录下，
/// 文件名以 texPath 的末段为前缀（如 BlackHole、BlackHole_a、BlackHole_b ...）。
/// </summary>
public class Graphic_Twinkle : Graphic_Collection
{
    private const float FramesPerSecond = 5f; // 每秒换 5 帧（真实秒）

    public override Material MatSingle => subGraphics[0].MatSingle;

    public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
    {
        if (thingDef == null)
        {
            return;
        }
        if (subGraphics == null || subGraphics.Length == 0)
        {
            Log.ErrorOnce("Graphic_Twinkle has no subgraphics: " + thingDef, 82160101);
            return;
        }
        Graphic graphic = subGraphics[FrameIndex(thing)];
        loc += data.DrawOffsetForRot(rot);
        Quaternion rotation = Quaternion.identity;
        if (extraRotation != 0f)
        {
            rotation = Quaternion.Euler(0f, extraRotation, 0f);
        }
        Graphics.DrawMesh(MeshAt(rot), loc, rotation, graphic.MatAt(rot, thing), 0);
    }

    private int FrameIndex(Thing thing)
    {
        // 用真实时间（墙钟）驱动，纯 FPS 动画：不受游戏速度（TPS）影响，暂停时也继续闪烁
        int t = (int)(Time.realtimeSinceStartup * FramesPerSecond);
        if (thing != null)
        {
            t += thing.thingIDNumber * 391;
        }
        int hash = t * -2048144789;
        int mod = hash % subGraphics.Length;
        return mod < 0 ? mod + subGraphics.Length : mod;
    }
}
