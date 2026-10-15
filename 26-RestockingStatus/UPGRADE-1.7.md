> 主代理游戏验证：`mod26-rev1248-b`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod26-rev1248-b`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

目标：1.7.4977 rev1248。1.4/1.5/1.6 内容保留，新增独立 net10.0 工程，引用 Krafs.Rimworld.Ref 1.7.4977-alpha.1 和 Lib.Harmony 3.0.0-rc.2，移除未使用的 HugsLib 编译模板依赖；无私有直接访问，不需 Publicizer。新代码显式选择自身程序集 PatchAll。

检查的游戏接口包括 Settlement.ExpandingIcon / NextRestockTick、Settlement_TraderTracker、Faction.def/FactionIcon、Faction.RelationWith(allowNull:true)、FactionRelationKind、TickManager.TicksGame，均与实际1.7合同及调用链吻合。没有修改补货判定及缺关系时的原处理。

纹理接口可编译但语义发生变化：UnityStubs Graphics.DrawTexture 在 RenderTexture 路径通过 Graphics.DrawTextureGpu→GpuTextureService.BlitTexture 复制像素，未做源 alpha 合成；无GPU路径也无法供应 RenderTexture.ReadPixels。旧方法依赖该组合生成刷新覆盖图标。仅新1.7将 TextureUtilities 的三项处理改为可读取纹理上的双线性缩放及 CPU source-over alpha合成，保留公开函数及实际底部右侧放置规则，复用现有派系缓存；不新增依赖或修改游戏 DLL。

依据：RW/17/UnityStubs/UnityEngine/Graphics.cs:660，Texture2D.GetPixels/GetPixelBilinear/SetPixels/Apply 的真实实现，以及实际调用只来自 Start.GetFactionRestockingIcon。GetPixelBilinear 的1.7实现按重复边缘采样；本MOD默认将刷新图标扩展到背景同尺寸，透明边缘限制保留在验证记录中。

所有可静态发现的编译引用记录在工作区 logs/26-RestockingStatus/api-audit.json。Release 编译通过。Tests/1.7/Mod26Checks.cs 在真实游戏测试驱动中核验缩放、透明混合/背景保留、位置、缓存、具体补丁注册和实际 Settlement.ExpandingIcon 的补货/未补货分支；首次记录时等待运行。GUI最终可见效果及保存/重载另行标注，不能用像素数据断言代替肉眼预览。
