# Colony Groups Targetable Portraits 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 [LTO] Colony Groups 增加"能力选目标模式下点击顶部头像/组弹窗头像选择对应小人"的能力。

**Architecture:** 一个 Harmony prefix patch（`RimWorld.ColonistBar.TryGetEntryAt`），把命中测试转发给
TacticalGroups 自己的 `TacticalColonistBar.TryGetEntryAt`（主栏）与 `TryGetGroupPawnAt`（悬停组弹窗），
其余目标校验/高亮/音效/shift 连选全部由 vanilla `Targeter` 自动完成。

**Tech Stack:** C# / net472 / Harmony 2.3 / Krafs.Rimworld.Ref 1.6.4488-beta / Krafs.Publicizer 2.*，
编译期引用 TacticalGroups.dll（不分发）。

**规格:** 同目录 `proposal.md`（根因分析与验证清单以它为准）。

## Global Constraints

- MOD 目录：`/home/lisanhu/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits/`（下文称 `$MOD`）
- packageId `RunningBugs.ColonyGroupsTargetablePortraits`，author `RunningBugs`（沿用仓库惯例）
- AssemblyName/RootNamespace `CGTargetablePortraits`
- `supportedVersions` 仅标 1.6；代码须保持跨版本兼容（不用 1.6 独有 API）
- 仅 patch `RimWorld.ColonistBar.TryGetEntryAt`，不 patch TG 自身任何方法，不碰 Targeter
- TacticalGroups.dll 仅编译期引用（`<Private>false</Private>`），不得拷入 `1.6/Assemblies/`
- dotnet 路径：`/home/lisanhu/.dotnet/dotnet`
- git commit 步骤执行前必须先征得用户确认

---

### Task 1: 项目骨架与编译环境打通

**Files:**
- Create: `$MOD/About/About.xml`
- Create: `$MOD/1.6/Source/mod.csproj`
- Create: `$MOD/1.6/Source/mod.sln`
- Create: `$MOD/1.6/Source/Placeholder.cs`（临时，Task 2 删除）
- Create: `$MOD/1.6/Source/TacticalGroups.dll`（从 workshop 目录拷贝，仅编译期引用）

**Interfaces:**
- Produces: 可编译的工程骨架；后续任务在 `1.6/Source/` 下加代码即可 `dotnet build` 输出到 `1.6/Assemblies/CGTargetablePortraits.dll`。

- [ ] **Step 1: 创建目录与 About.xml**

```bash
mkdir -p /home/lisanhu/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits/{About,1.6/Assemblies,1.6/Source}
```

`$MOD/About/About.xml` 内容：

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
	<packageId>RunningBugs.ColonyGroupsTargetablePortraits</packageId>
	<name>Colony Groups Targetable Portraits</name>
	<author>RunningBugs</author>
	<supportedVersions>
		<li>1.6</li>
	</supportedVersions>

	<modDependencies>
		<li>
			<packageId>brrainz.harmony</packageId>
			<displayName>Harmony</displayName>
			<steamWorkshopUrl>steam://url/CommunityFilePage/2009463077</steamWorkshopUrl>
			<downloadUrl>https://github.com/pardeike/HarmonyRimWorld/releases/latest</downloadUrl>
		</li>
		<li>
			<packageId>DerekBickley.LTOColonyGroupsFinal</packageId>
			<displayName>[LTO] Colony Groups</displayName>
			<steamWorkshopUrl>https://steamcommunity.com/sharedfiles/filedetails/?id=2345493945</steamWorkshopUrl>
		</li>
	</modDependencies>

	<loadAfter>
		<li>brrainz.harmony</li>
		<li>DerekBickley.LTOColonyGroupsFinal</li>
	</loadAfter>

	<description>
让 [LTO] Colony Groups 的顶部头像栏和悬停组弹窗头像支持能力选目标：激活需要选目标的技能（如异常医术）后，直接点击头像即可对该小人施法，行为与 vanilla ColonistBar 一致（校验、高亮、音效、shift 连选）。

Makes [LTO] Colony Groups' top-bar portraits and hover group-popup portraits work as ability targets: while targeting an ability (e.g. Unnatural Healing), click a portrait to target that pawn, with full vanilla behavior (validation, highlight, sound, shift multi-select).
	</description>
</ModMetaData>
```

- [ ] **Step 2: 拷贝 TacticalGroups.dll 并编写 csproj/sln**

```bash
cp /Data/SteamLibrary/steamapps/workshop/content/294100/2345493945/1.6/Assemblies/TacticalGroups.dll \
   /home/lisanhu/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits/1.6/Source/TacticalGroups.dll
```

`$MOD/1.6/Source/mod.csproj` 内容：

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<OutputType>Library</OutputType>
		<TargetFramework>net472</TargetFramework>
		<PlatformTarget>x64</PlatformTarget>
		<RootNamespace>CGTargetablePortraits</RootNamespace>
		<AssemblyName>CGTargetablePortraits</AssemblyName>
		<OutputPath>../Assemblies</OutputPath>
		<VersionPrefix>0.1.0.0</VersionPrefix>
		<DebugType>none</DebugType>
		<DebugSymbols>false</DebugSymbols>
		<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
		<CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
		<LangVersion>11.0</LangVersion>
	</PropertyGroup>
	<PropertyGroup>
		<PublicizeAll>true</PublicizeAll>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Krafs.Publicizer" Version="2.*">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
		</PackageReference>
		<PackageReference Include="Krafs.Rimworld.Ref" Version="1.6.4488-beta" />
		<PackageReference Include="Lib.Harmony" Version="2.3.*" />
		<Reference Include="TacticalGroups">
			<HintPath>TacticalGroups.dll</HintPath>
			<Private>false</Private>
		</Reference>
	</ItemGroup>
</Project>
```

`$MOD/1.6/Source/mod.sln` 内容（GUID 固定如下即可）：

```

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "mod", "mod.csproj", "{7C3A1B2E-4F5D-4E6A-9B8C-1D2E3F4A5B6C}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7C3A1B2E-4F5D-4E6A-9B8C-1D2E3F4A5B6C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7C3A1B2E-4F5D-4E6A-9B8C-1D2E3F4A5B6C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7C3A1B2E-4F5D-4E6A-9B8C-1D2E3F4A5B6C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7C3A1B2E-4F5D-4E6A-9B8C-1D2E3F4A5B6C}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
```

- [ ] **Step 3: 写占位类并验证编译链路**

`$MOD/1.6/Source/Placeholder.cs` 内容：

```csharp
namespace CGTargetablePortraits
{
    internal static class Placeholder
    {
    }
}
```

Run: `cd /home/lisanhu/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits/1.6/Source && /home/lisanhu/.dotnet/dotnet build -c Release`

Expected: `Build succeeded`，产物 `$MOD/1.6/Assemblies/CGTargetablePortraits.dll` 存在，
且 `1.6/Assemblies/` 下**没有** TacticalGroups.dll（`Private=false` 生效）。

- [ ] **Step 4: Commit（执行前征得用户确认）**

```bash
cd /home/lisanhu/mine/workspace/rimworld/RimModNotes
git add 76-ColonyGroupsTargetablePortraits
git commit -m "Add ColonyGroupsTargetablePortraits project skeleton"
```

---

### Task 2: Harmony patch 实现

**Files:**
- Create: `$MOD/1.6/Source/ColonistBarTryGetEntryAtPatch.cs`
- Delete: `$MOD/1.6/Source/Placeholder.cs`

**Interfaces:**
- Consumes: Task 1 的可编译骨架。
- Produces: `CGTargetablePortraits.dll`，加载后 prefix `RimWorld.ColonistBar.TryGetEntryAt`。

实现依据（已反编译核实）：
- vanilla `Targeter.CurrentTargetUnderMouse` 会调 `Find.ColonistBar.TryGetEntryAt(UI.MousePositionOnUIInverted, out entry)`，命中后用 `entry.pawn` 作为目标；
- TG 未 patch 该方法，且 TG 环境下其唯一活着的调用方就是 Targeter；
- TG 侧入口均 public：`TacticUtils.TacticalColonistBar`（静态字段）、`TacticalColonistBar.TryGetEntryAt(Vector2, out TacticalColonistBar.Entry)`（Entry 有 public 字段 pawn/map/group）、`TacticalColonistBar.TryGetGroupPawnAt(Vector2, out Pawn)`。

- [ ] **Step 1: 删除占位类，编写 patch**

删除 `$MOD/1.6/Source/Placeholder.cs`，新建 `$MOD/1.6/Source/ColonistBarTryGetEntryAtPatch.cs`：

```csharp
using HarmonyLib;
using RimWorld;
using TacticalGroups;
using UnityEngine;
using Verse;

namespace CGTargetablePortraits
{
    [StaticConstructorOnStartup]
    public static class ModEntry
    {
        static ModEntry()
        {
            new Harmony("RunningBugs.ColonyGroupsTargetablePortraits").PatchAll();
        }
    }

    /// <summary>
    /// [LTO] Colony Groups 替换了 vanilla ColonistBar 的绘制，但没有 patch
    /// ColonistBar.TryGetEntryAt，导致 Targeter 选目标时无法命中 TG 的头像。
    /// 本 patch 把命中测试转发给 TG 自己的实现：主栏 TryGetEntryAt，
    /// 失败则 fallback 到悬停组弹窗的 TryGetGroupPawnAt。
    /// </summary>
    [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.TryGetEntryAt))]
    public static class ColonistBarTryGetEntryAtPatch
    {
        public static bool Prefix(Vector2 pos, out ColonistBar.Entry entry, ref bool __result)
        {
            TacticalColonistBar bar = TacticUtils.TacticalColonistBar;
            if (bar == null)
            {
                // TG 未就绪（如主菜单阶段）：放行原逻辑
                entry = default;
                return true;
            }
            if (bar.TryGetEntryAt(pos, out TacticalColonistBar.Entry tgEntry))
            {
                entry = new ColonistBar.Entry(tgEntry.pawn, tgEntry.map, tgEntry.group);
                __result = true;
                return false;
            }
            if (bar.TryGetGroupPawnAt(pos, out Pawn popupPawn))
            {
                entry = new ColonistBar.Entry(popupPawn, popupPawn.Map, 0);
                __result = true;
                return false;
            }
            // TG 环境下 vanilla 命中测试本就无意义，直接不命中
            entry = default;
            __result = false;
            return false;
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `cd /home/lisanhu/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits/1.6/Source && /home/lisanhu/.dotnet/dotnet build -c Release`

Expected: `Build succeeded`，0 warning 0 error（Harmony/`nameof` 解析正常说明引用正确）。

- [ ] **Step 3: 静态 sanity check（可选但推荐）**

Run: `/home/lisanhu/.dotnet/tools/ilspycmd -t CGTargetablePortraits.ColonistBarTryGetEntryAtPatch /home/lisanhu/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits/1.6/Assemblies/CGTargetablePortraits.dll | head -20`

Expected: 能反编译出 patch 类，确认 DLL 内容有效。

- [ ] **Step 4: Commit（执行前征得用户确认）**

```bash
cd /home/lisanhu/mine/workspace/rimworld/RimModNotes
git add 76-ColonyGroupsTargetablePortraits
git commit -m "Implement ColonistBar.TryGetEntryAt patch for Colony Groups targeting"
```

---

### Task 3: 部署与游戏内验证

**Files:**
- Modify: `/Data/SteamLibrary/steamapps/common/RimWorld/Mods/`（新增一个软链接，非仓库文件）

**Interfaces:**
- Consumes: Task 2 产出的 `1.6/Assemblies/CGTargetablePortraits.dll`。

- [ ] **Step 1: 建立软链接**

```bash
ln -s /home/lisanhu/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits \
      /Data/SteamLibrary/steamapps/common/RimWorld/Mods/76-ColonyGroupsTargetablePortraits
ls -la /Data/SteamLibrary/steamapps/common/RimWorld/Mods/76-ColonyGroupsTargetablePortraits/About/About.xml
```

Expected: 软链接存在且能穿透读到 About.xml（与 01~75 号 MOD 同惯例）。

- [ ] **Step 2: 在 MOD 管理器（RimSort/游戏内）启用**

启用本 MOD，确认排序在 Harmony 与 [LTO] Colony Groups 之后。此步由用户操作。

- [ ] **Step 3: 游戏内验证清单（用户执行，逐项确认）**

按 proposal.md 验证清单：

1. 激活异常医术 → 点顶部头像 → 对该小人排队施法，有确认音效与高亮
2. 悬停展开组弹窗 → 点弹窗小头像 → 同样生效
3. shift + 点头像 → MultiSelect 能力连续选多目标
4. Esc / 右键 → 取消选目标
5. 非选目标时头像原行为（选中、双击跳镜头、右键菜单、拖拽排序）不受影响
6. 检查 `~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Player.log` 无红字

- [ ] **Step 4: 验证通过后归档（spec-flow）**

按 spec-flow 归档动作：把规格沉淀到 `$MOD/docs/specs/`（status: current），
删除 `$MOD/docs/changes/colony-groups-targetable-portraits/`，勾选 tasks.md 后一并归档，
向用户一句话汇报归档内容。Commit 前征得用户确认。
