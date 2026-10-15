> 主代理游戏验证：`mod30-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod30-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod30-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod30-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod30-functional`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod30-functional`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

基线：1.7.4977 rev926。修改类型：仅在 About 的 supportedVersions 增加 1.7；旧版本内容与补丁完全保留，无 C# 源码、编译或 publicize 需求。

唯一活跃补丁 `Common/Patches/Patches.xml` 使用 PatchOperationSequence / PatchOperationReplace，在 Biotech 启用时将 `Toxifier_Working` 的所有 subSounds/li/volumeRange 替换为 0。1.7 原版目标仍在 `Data/Biotech/Defs/SoundDefs/Building_Sustainers_Ambiences.xml:180`。真实 `Verse.SoundDef.subSounds` 和 `Verse.Sound.SubSoundDef.volumeRange` 仍存在；后者的声音增益计算仍使用 range.RandomInRange / 100，0 范围可保持静音语义。

默认 Common 加根目录加载规则可继续使用，无需新增 LoadFolders 或复制 1.7 文件夹。所有活跃 XML 解析通过；不存在自有 C# 函数，单元测试不适用。

游戏内断言为 `Tests/1.7/Mod30Checks.cs`，检查实际 DefDatabase 已加载的目标声音每个子声音均为 0～0。由工作区测试驱动编译并执行，首次记录时尚待运行；无窗口断言验证增益字段效果，不代表已测试音频设备输出。
