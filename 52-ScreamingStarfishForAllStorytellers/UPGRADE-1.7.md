> 主代理游戏验证：`mod52-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod52-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod52-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod52-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已静态适配，运行功能验证未完成。基线 `1.7.4977 rev926`；发布 ID `3493262370`；packageId `RunningBugs.ScreamingStarfishForAllStorytellers`。

最小变更仅 About 在原 1.5/1.6 后添加 1.7。原 packageId/名称/发布 ID、依赖和 loadAfter、Common Patch 及全部旧内容逐字节保留，不改 LoadFolders.xml.bak、不创建空版本目录。本 MOD 无生产 C#、DLL、自有存档字段，Common Def/语言只是空容器。新增测试代码不进入发布程序集。

唯一活跃 PatchOperationFindMod 按显示名 ScreamingStarfish 查找上游，随后给缺少 `ScreamingStarfish.StorytellerCompProperties_PostSounds` 的 StorytellerDef/comps 添加该属性。实际 1.7 PatchOperationFindMod 仍按 ModLister.HasActiveModWithName 匹配，PatchOperationAdd 仍按 XPath 添加；StorytellerDef.comps、StorytellerCompProperties.compClass 和 Storyteller.InitializeStorytellerComps 的实例化路径仍有效，未发现本补丁接口改变。

上游已安装 `/Data/SteamLibrary/steamapps/workshop/content/294100/3493059014`，packageId `ZM.ScreamingStarfish`，显示名准确为 ScreamingStarfish，支持到 1.6。其显式 LoadFolders 在 1.7 回退 v1.6；不启用可选 Mumu.Starfish 时选择 `1.6/ScreamingStarfish`，另需 Harmony。主代理测试需使用已匹配的 Harmony 3，核验旧上游 DLL 的实际加载；未升级或改写 Workshop 上游。

已读取上游所附组件源码，并反编译实际 `1.6/ScreamingStarfish/Assemblies/ScreamingStarfish.dll` 核验：属性构造 compClass 指向 StorytellerComp_PostSounds；该组件仅为空标记；PlaySoundAfterReceiveLetterPatch 的实际私有 gate 枚举当前 storytellerComps 并以标记存在返回 bool。原生信件入口仍是 `LetterStack.ReceiveLetter(Letter,string,int,bool)`，上游 postfix 查 gate 后修改实际 SoundDef 音量并播放。真实 1.7 保留该入口，静态链条有效。上游采用显式 Assembly 的 PatchAll，没有共享文档所述无参风险。

需要保留的现有边界：XPath 也命中抽象 BaseStoryteller，1.7 XML 继承会追加列表，因此部分叙事者可能同时有继承/直属的两个空标记。此风险来自原 Patch，不是本轮添加；上游实际 gate 只返回 bool，不按组件数重复播放。测试记录数量并验证实际一次信件钩子，不顺手重构旧补丁。未测试任意第三方叙事者、没有 comps 的自定义 Def 或可选 Mumu.Starfish 联动。

已执行 XML 解析、上游名称/类型/目录核验、官方原始 XPath 匹配与全部旧文件字节检查，证据 `logs/52-ScreamingStarfishForAllStorytellers/static-validation.json`，实际上游反编译见 `upstream-patch.cs`；保留 DLL SHA-256。纯 XML 无生产编译或虚构函数单元测试。

测试专用 `Tests/1.7/Mod52Checks.cs` 使用共享 Map 驱动接口，只引用游戏与 Harmony 3，通过反射访问真实上游。它验证每个实际 StorytellerDef 及构造后的组件、上游 gate、具体 Harmony postfix 注册，发送可丢弃真实信件并检查实际音量修改；覆盖关闭设置与移除标记的边界，以 finally 恢复叙事者/设置/音量并移除测试信件。驱动已精准 Ref/Harmony 编译，最终 0 warnings/errors（checks-build.log），实际执行仍 pending。

待主代理：上游+Harmony 基线，补丁单体验证并执行 Mod52Checks；核对没有新增 XML/Harmony/UnityStubs 异常。Headless 只能证明真实钩子与声音数据修改，不能证明实际音频输出/听感。保存重载和可选海星联动另需相应夹具。当前不能以成功编译测试或可用标签宣称运行功能已通过。
