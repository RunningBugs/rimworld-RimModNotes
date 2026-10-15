> 主代理游戏验证：`mod19-rev1248-i`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod19-rev1248-i`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**独立 1.7 编译完成；上游加载、渲染和黑眼圈修复待验证**。保留原 Source/1.4/DLL、packageId、发布 ID；About 新增 1.7，默认新版本目录不双载旧 DLL。

## API 与实际图形流程

本补丁真实功能只有 `TextureReDraw : GameComponent` 的 GameComponentTick 首次调用实际 `FacialAnimation.MyGraphicPool.RepaintAllGraphic()`，成功后 runOnce=true；新 game component 会重新执行，runOnce 未 Scribe，旧存档也没有这一字段迁移。静态启动只记录日志，没有自有 Harmony/HugsLib 使用。GameComponent(Game) 构造发现机制、GameComponentTick 虚方法和 Verse.Log 仍存在，完整静态引用见 api-references.json。新生产 C# 逐字复用原文件；net10.0/精准 Krafs.Rimworld.Ref 1.7.4977-alpha.1，移除未用 Harmony/HugsLib 模板引用，不需要 Publicizer。

当前已安装上游 `nals.facialanimation` / Workshop1635901197 只到 1.6，实际选择1.6/Assemblies/FacialAnimation.dll，固定真实参考和 SHA 记录 dependencies.json，Private=false，不输出引用 DLL。旧标签不足证明不兼容，root 应先做上游基线；不要修改上游或用桩测试。

MyGraphicPool.RepaintAllGraphic 第10–14行确实遍历 dict.Values 并调用 Graphic_RenderTexture.Paint；Count 和 Register 仍存在。Graphic_RenderTexture.Paint 取真实纹理、旋转材质、RenderTexture、NLCommon.AfterEffectUtils.RenderWithFunc/Draw，而 RenderCamera.RenderBegin 调 Camera.Render→OnPostRender→renderFunc，因此仍是有效重绘流程。对应选定类型反编译在 logs/19-FacialAnimationEyeFix。最新 Waylith UnityStubs/UnityEngine/Camera.cs:537–569 确实派发 OnPreCull/OnPreRender/OnPostRender 并将捕获 mesh 提交 ICameraTextureRenderer；不是空 Render 桩。渲染实现仍须有窗口检验，headless 无 GPU 不能证明生成正确眼睛纹理。

## 验证范围

`bash scripts/dotnet.sh build work/19-FacialAnimationEyeFix/1.7/Source/mod.csproj -c Release --no-restore` 0 warning/error（build.log）；Tests/1.7/Mod19Checks.cs 同样编译通过。它检查实际 game 已发现本 component，并给实际上游 RepaintAllGraphic 添加只计数、不跳过原方法的临时观察 Prefix，在 fresh component 两次 Tick 下要求只调用一次真实上游，finally 仅卸载自己的具体观察方法。输出真实 pool.Count 并明确渲染结果仍待测。该场景只证明调用时机，不能充当黑眼圈像素修复的功能通过，pool=0 更不能代表渲染成功。未运行游戏；没有独立可有效脱离引擎测试的算法，不制造实现镜像单元测试。

root 递归 runtime IDs `brrainz.harmony`（上游真实用 Harmony）、`nals.facialanimation`，后者路径 `/Data/SteamLibrary/steamapps/workshop/content/294100/1635901197`。待有窗口场景：有 facial graphics 的 pawn、确保 pool 非空，首个 tick 重绘后眼睛遮罩/黑色叠层正常，重复 tick 不重复重绘；新游戏、存档重载、多个 face style/其他渲染MOD分别核验。Shader/material/RenderTexture 正确性和上游整体 1.7 兼容尚未验证。
