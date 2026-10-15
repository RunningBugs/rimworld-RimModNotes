> 主代理游戏验证：`mod12-rev1248-h`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod12-rev1248-h`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录：已适配，完整存档流程待验证

游戏基线 `1.7.4977 rev1248`。必要 RimSaves `arandomkiwi.rimsaves` 的实际 1.6 DLL、More Autosave Slots (Continued) `mlie.moreautosaveslots` 的实际 1.6 DLL均仅声明到 1.6，没有已确认的 1.7 自动保存完整流程。另有本机同名修改副本 `mlie.moreautosaveslots.runningbugs`（在范围外），不能擅自更新或同时启用两个同名程序集。两个已安装上游的旧支持声明本身不构成阻塞；实际目标合同存在且语义已核实，现已生成本补丁 1.7 候选并新增 About 支持。旧版本源码/DLL 不变，运行兼容由主代理先验证上游基线。

## 实际接口与语义

旧 1.6 工程使用 1.4.3704 游戏参考 / Harmony 2.2.2 / net481，工程目录名称不是真实引用版本。`Settings.curFolder` 在实际 RimSaves DLL 为公有静态 string（反编译 Settings 第 12 行，Scribe 第 256 行）；真实分隔符 `#§#` 用于平铺文件名的虚拟文件夹。补丁在 SavedGameNamedExists/FilePathForSavedGame 前补此前缀，避免两个虚拟目录使用同一槽位。

真实 MoreAutosaveSlotsSettings 的 `private static string[] autoSaveNames(bool onlyOne=false)` 第 48 行和 `public static string NextName()` 第 123 行仍存在，NextName 先选择未使用的候选，再按文件 LastWriteTime 选择最老文件（第 125–126 行）。本补丁 Prefix 完全替代 NextName，并反射 autoSaveNames(false)，保持目录局部的轮换语义。上游 Autosaver_NewAutosaveFileName Prefix 调用 NextName，是实际活跃入口；这条自动存档流水线仍须在 1.7 实测，不能只验证命名方法。

完整旧产物静态引用 `logs/12-RimSavesMoreAutoaveSlotsPatch/api-references-baseline.json`，两个选定类型及 RimSaves 完整反编译留在该日志目录。当前真实游戏 `Verse.GenFilePaths` 的 AllSavedGameFiles（216）和 FilePathForSavedGame（355）存在；文件枚举、System.IO、时间比较、Linq、Harmony/AccessTools 使用均已审查。私有方法用反射，因此 Publicizer 不是必需。HugsLib 是未使用模板编译引用，后续新工程可以移除。

旧实现 autoSaveNames 缺失时返回空数组，却继续 MinBy，可能出现空数组异常；本轮因上游阻塞不顺手修改既有逻辑。无自有存档字段/Def XML 迁移。所有实际接口来自安装 DLL，未使用桩或仓库 MAS 反编译副本冒充运行依赖。

## 1.7 产物、命令及待测项

独立 `1.7/Source`，net10.0 + 精准 Krafs.Rimworld.Ref 1.7.4977-alpha.1 / Lib.Harmony 3.0.0-rc.2。复制**当前实际安装**两个上游 DLL 为固定编译参考 Private=false（不进入 Assemblies），新工程移除未用 HugsLib、显式 `PatchAll(typeof(Start).Assembly)`。所有目录局部选择逻辑保持原样；.NET 10 的 System.Linq.MinBy 存在，因此原调用编译成立。生产编译 0 warning/error，`build.log`；全部新 DLL 静态引用 `api-references.json`。命令 `bash scripts/dotnet.sh build work/12-RimSavesMoreAutoaveSlotsPatch/1.7/Source/mod.csproj -c Release --no-restore`。

`Tests/1.7/Mod12Checks.cs` 编译通过，只引用 game/Harmony、实际上游通过反射。要求 savedatafolder 在本工作区 testing/cases 下才允许写入：临时唯一槽名实际文件、Default 空槽、文件夹 A 已用槽选 next、文件夹 B 不串槽、A 全满选最老真实 LastWriteTime、Default 不见虚拟目录文件。调用**真实上游已打补丁的 NextName**和 RimSaves 已打补丁的 AllSavedGameFiles，非直接调用自有 Prefix；最后恢复 curFolder/私有 count/format 并删除夹具文件。文件不是有效存档，不尝试加载。该场景验证实际目录选择链，但不宣称完成保存/重新加载；没有与它重复的机械单元测试。

递归实际运行依赖 `brrainz.harmony`、`arandomkiwi.rimsaves`（Workshop1713367505）、`mlie.moreautosaveslots`（Workshop3410766209）；只选 Workshop MAS，不启用范围外同名本机副本。记录所用 DLL SHA 见 dependencies.json。两上游都对 Autosaver.NewAutosaveFileName 安装返回 false 的 Prefix，注册顺序/优先级可能决定谁接管外层流程，必须在上游基线检查真实调用；本轮未掩盖此既有组合风险。完整自动保存、目录切换 UI、屏幕截图钩子、保存/重载以及两者外层 Prefix 的有效优先级待 root 串行验证。未运行游戏，未触碰真实存档、Workshop 或源仓库。

独立整合审核补充：1.7新DLL硬引用Mlie MoreAutosaveSlots，但原About只有loadAfter。为v1.7显式声明已验证的RimSaves、mlie.moreautosaveslots和Harmony三前置，旧base依赖/版本不改；产品DLL不变。XML解析与当前VersionedList规则已核验，完整组合smoke将观察真实有效依赖。


## 2026-10-15 名称和描述版本修订

本轮在现有1.7适配基础上更新About名称/描述版本措辞，packageId/发布ID/支持标签/生产源码与DLL不改。

- 明确Template占位；依据实际NextName/GenFilePaths补丁及目录轮换验证填写功能，不宣称完整自动保存/重载均验证。

语法/身份/版本声明与原产物一致已核验；属于元数据修改，不将其当新功能验证。详细回写审计见工作区expansion/metadata-changes.json。
