# KillingReward 嗜血恩赐 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新建 RimWorld 1.6 Mod「KillingReward 嗜血恩赐」：我方小人亲手击杀敌对派系单位积累进度，进度打满获得奖励次数，可在奖励窗口中兑换（立刻完成研究 / 技能 +3 / 一整格物品）。

**Architecture:** Harmony postfix 监听 `Pawn.Kill`，纯逻辑（进度曲线、状态机、击杀判定）放在无 Verse 依赖的 `Source/Core/` 下以便 xunit 单测；Verse 适配层、GameComponent、UI（IMGUI Window + MainButton + ChoiceLetter）在 `Source/` 下。UI 布局先用 rimworld-imgui-sim 离线渲染 mockup 再落地。

**Tech Stack:** C# net472 + Krafs.Rimworld.Ref 1.6.4488-beta + Lib.Harmony 2.3.1.1；xunit(net8.0) 单元测试；python 静态白盒测试；rimworld-imgui-sim（位于 `~/mine/workspace/rimworld/rimworld-imgui-sim`）做 UI mockup。

**Spec:** `77-KillingReward/docs/2026-07-31-KillingReward-design.md`（含全部双语字符串的「文案与基调」一节）

## Global Constraints

- 目录：`77-KillingReward/`；packageId `RunningBugs.KillingReward`；仅支持 1.6。
- 全部文案双语：英文 + 简体中文，代码内不写死显示字符串（Keyed 翻译；Def 的 label 走 DefInjected）。
- 纯逻辑（`Source/Core/*.cs`）禁止引用 Verse/RimWorld/UnityEngine，命名空间 `KillingReward.Core`。
- csproj 约定：net472、`<OutputPath>../Assemblies</OutputPath>`、`DebugType=none`、LangVersion 11.0（参照 `16-ResearchPrerequisites/1.6/Source/mod.csproj`，但不要 HugsLib）。
- 提交风格：与仓库历史一致的英文简短 commit。
- 设计文档中的字符串表格是文案事实源，实现时 key 名按下表，字符串逐字采用设计文档「文案与基调」。

### Keyed 翻译 key 一览（实现时两个 Keys.xml 内容一致，仅语言不同）

| key | 中文 | English |
| --- | --- | --- |
| KR_LetterTitle | 嗜血恩赐 | Blood Boon |
| KR_LetterText | 它在黑暗中注视着每一场杀戮，你的挣扎令它非常满意。这份恩赐是你应得的。领取它——然后，尽量别死。 | The dark archotech watches every kill, and your struggle amuses it greatly. This boon is yours. Claim it — and try to stay alive. |
| KR_LetterOpen | 接受恩赐 | Accept the Boon |
| KR_WindowTitle | 黑暗超凡智能的恩赐 | Boon of the Dark Archotech |
| KR_Progress | 血祭 | Blood Tithe |
| KR_Tier | 恩赐等阶 | Boon Tier |
| KR_Pending | 待领取的恩赐 | Unclaimed Boons |
| KR_RewardResearch | 禁忌知识 | Forbidden Knowledge |
| KR_RewardResearchDesc | 它将知识直接烙进学者的脑海。立刻完成一项当前可研究的科技。 | It sears knowledge straight into your scholars' minds. Instantly complete one available research project. |
| KR_RewardSkill | 技艺灌注 | Bestowed Prowess |
| KR_RewardSkillDesc | 它替你拨动了神经与肌肉。选择一名小人，其一项技能提升 3 级。 | It plucks the strings of nerve and muscle. Choose a pawn; one of their skills gains 3 levels. |
| KR_RewardItem | 虚空馈赠 | Gift from the Void |
| KR_RewardItemDesc | 它从虚空中掷下物资。选择一种物品与投放地点，领取一整格。 | It casts provisions from the void. Choose an item and a drop cell to receive a full stack. |
| KR_Claimed | 恩赐已兑现。它仍在注视——继续取悦它吧。 | The boon is granted. It is still watching. Keep it entertained. |
| KR_NoPending | 它还没有新的恩赐。杀戮即是祈祷。 | It has no boon for you yet. Slaughter is prayer. |
| KR_SettingInitial | 初始击杀要求 | Initial kills required |
| KR_SettingInitialDesc | 第一次升级所需的击杀数。 | Kills required for the first reward tier. |
| KR_SettingMode | 增长模式 | Growth mode |
| KR_SettingModeExponential | 指数（每级 ×倍率） | Exponential (× factor per tier) |
| KR_SettingModeLinear | 线性（每级 +增量） | Linear (+ increment per tier) |
| KR_SettingFactor | 指数倍率 | Exponential factor |
| KR_SettingFactorDesc | 指数模式下，每级要求变为上一级的该倍率。 | In exponential mode, each tier costs this factor times the previous one. |
| KR_SettingIncrement | 线性增量 | Linear increment |
| KR_SettingIncrementDesc | 线性模式下，每级比上一级多要求的击杀数。 | In linear mode, each tier costs this many more kills than the previous one. |
| KR_PickProject | 选择要完成的研究 | Choose a research to complete |
| KR_PickPawn | 选择一名小人 | Choose a pawn |
| KR_PickSkill | 选择一项技能 | Choose a skill |
| KR_PickItem | 选择一种物品 | Choose an item |
| KR_PickCell | 点击一个格子投放物资 | Click a cell to deliver the goods |
| KR_Back | 返回 | Back |
| KR_ItemDelivered | 虚空馈赠已送达。 | The gift from the void has arrived. |
| KR_Offering | 祭品+1 | Offering +1 |

---

### Task 1: 项目脚手架 + 构建 + 软链接部署

**Files:**
- Create: `77-KillingReward/About/About.xml`
- Create: `77-KillingReward/LoadFolders.xml`
- Create: `77-KillingReward/1.6/Source/mod.csproj`
- Create: `77-KillingReward/1.6/Source/KillingRewardMod.cs`
- Create: `77-KillingReward/1.6/Source/KillingRewardSettings.cs`

**Interfaces:**
- Produces: `KillingReward.KillingRewardMod : Mod`，静态属性 `KillingRewardMod.Settings`（类型 `KillingRewardSettings`）；`KillingRewardSettings` 字段 `int InitialKills=10`、`GrowthMode Mode=Exponential`、`float ExponentialFactor=1.2f`、`int LinearIncrement=10`；`KillingReward.Core.GrowthMode` 枚举（Step 4 创建）。
- Produces: 构建产物 `77-KillingReward/1.6/Assemblies/KillingReward.dll`；游戏 Mods 目录软链接。

- [ ] **Step 1: 写 About/About.xml**

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
	<packageId>RunningBugs.KillingReward</packageId>
	<name>KillingReward 嗜血恩赐</name>
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
	</modDependencies>
	<description>
Kills by your own colonists against hostile factions fill a blood tithe. Each filled tithe earns a boon from the dark archotech: instantly complete a research, raise a pawn's skill by 3, or receive a full stack of an item of your choice.

我方小人亲手击杀敌对派系单位可积累「血祭」进度。进度打满即可获得黑暗超凡智能的恩赐：立刻完成一项研究、小人技能 +3、或领取一整格指定物品。
	</description>
</ModMetaData>
```

- [ ] **Step 2: 写 LoadFolders.xml**

```xml
<?xml version="1.0" encoding="utf-8"?>
<loadFolders>
	<v1.6>
		<li>/</li>
		<li>1.6</li>
	</v1.6>
</loadFolders>
```

- [ ] **Step 3: 写 1.6/Source/mod.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<OutputType>Library</OutputType>
		<TargetFramework>net472</TargetFramework>
		<PlatformTarget>x64</PlatformTarget>
		<RootNamespace>KillingReward</RootNamespace>
		<AssemblyName>KillingReward</AssemblyName>
		<OutputPath>../Assemblies</OutputPath>
		<VersionPrefix>0.1.0.0</VersionPrefix>
		<DebugType>none</DebugType>
		<LangVersion>11.0</LangVersion>
		<DebugSymbols>false</DebugSymbols>
		<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
		<CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Krafs.Rimworld.Ref" Version="1.6.4488-beta" />
		<PackageReference Include="Lib.Harmony" Version="2.3.1.1" />
	</ItemGroup>
</Project>
```

- [ ] **Step 4: 写 1.6/Source/Core/GrowthMode.cs**

```csharp
namespace KillingReward.Core
{
    public enum GrowthMode
    {
        Exponential,
        Linear
    }
}
```

- [ ] **Step 5: 写 1.6/Source/KillingRewardSettings.cs**

```csharp
using KillingReward.Core;
using Verse;

namespace KillingReward
{
    public class KillingRewardSettings : ModSettings
    {
        public int InitialKills = 10;
        public GrowthMode Mode = GrowthMode.Exponential;
        public float ExponentialFactor = 1.2f;
        public int LinearIncrement = 10;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref InitialKills, "InitialKills", 10);
            Scribe_Values.Look(ref Mode, "Mode", GrowthMode.Exponential);
            Scribe_Values.Look(ref ExponentialFactor, "ExponentialFactor", 1.2f);
            Scribe_Values.Look(ref LinearIncrement, "LinearIncrement", 10);
        }
    }
}
```

- [ ] **Step 6: 写 1.6/Source/KillingRewardMod.cs（设置界面留到 Task 6，本任务仅保证可构建）**

```csharp
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class KillingRewardMod : Mod
    {
        public static KillingRewardSettings Settings { get; private set; }

        public KillingRewardMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<KillingRewardSettings>();
        }

        public override string SettingsCategory()
        {
            return "KillingReward";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Task 6 填充完整设置界面
            base.DoSettingsWindowContents(inRect);
        }
    }
}
```

- [ ] **Step 7: 构建**

Run: `cd 77-KillingReward/1.6/Source && dotnet build -c Release`
Expected: `Build succeeded.` / `0 Error(s)`，生成 `../Assemblies/KillingReward.dll`

- [ ] **Step 8: 软链接到游戏 Mods 目录**

```bash
ln -sfn "$PWD/77-KillingReward" "/Data/SteamLibrary/steamapps/common/RimWorld/Mods/77-KillingReward"
ls -l "/Data/SteamLibrary/steamapps/common/RimWorld/Mods/77-KillingReward/About/About.xml"
```

Expected: 链接存在且能读到 About.xml（与仓库中 01、16 号 Mod 的部署方式一致）。

- [ ] **Step 9: Commit**

```bash
git add 77-KillingReward
git commit -m "Scaffold KillingReward mod (About, csproj, settings stub)"
```

---

### Task 2: ProgressCurve 进度曲线 + 单元测试

**Files:**
- Create: `77-KillingReward/1.6/Source/Core/ProgressCurve.cs`
- Create: `77-KillingReward/Tests/unit/KillingReward.UnitTests.csproj`
- Create: `77-KillingReward/Tests/unit/ProgressCurveTests.cs`

**Interfaces:**
- Produces: `KillingReward.Core.ProgressCurve.RequiredKills(GrowthMode mode, int initial, double factor, int increment, long completedLevels) -> long`。completedLevels 从 0 起：第 0 级（第一次升级）要求 = initial。

- [ ] **Step 1: 写测试工程**

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net8.0</TargetFramework>
		<IsPackable>false</IsPackable>
		<Nullable>disable</Nullable>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
		<PackageReference Include="xunit" Version="2.7.0" />
		<PackageReference Include="xunit.runner.visualstudio" Version="2.5.7" />
	</ItemGroup>
	<ItemGroup>
		<Compile Include="../../1.6/Source/Core/*.cs" />
	</ItemGroup>
</Project>
```

- [ ] **Step 2: 写失败测试**

```csharp
using KillingReward.Core;
using Xunit;

namespace KillingReward.UnitTests
{
    public class ProgressCurveTests
    {
        [Fact]
        public void Exponential_LevelZero_EqualsInitial()
        {
            Assert.Equal(10L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 1.2, 99, 0));
        }

        [Fact]
        public void Exponential_GrowsByFactorWithRounding()
        {
            // 10 * 1.2^1 = 12, 10 * 1.2^2 = 14.4 -> 14, 10 * 1.2^3 = 17.28 -> 17
            Assert.Equal(12L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 1.2, 99, 1));
            Assert.Equal(14L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 1.2, 99, 2));
            Assert.Equal(17L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 1.2, 99, 3));
        }

        [Fact]
        public void Linear_GrowsByIncrement()
        {
            Assert.Equal(10L, ProgressCurve.RequiredKills(GrowthMode.Linear, 10, 1.2, 5, 0));
            Assert.Equal(15L, ProgressCurve.RequiredKills(GrowthMode.Linear, 10, 1.2, 5, 1));
            Assert.Equal(60L, ProgressCurve.RequiredKills(GrowthMode.Linear, 10, 1.2, 5, 10));
        }

        [Fact]
        public void HugeLevel_DoesNotOverflow()
        {
            long v = ProgressCurve.RequiredKills(GrowthMode.Exponential, 10, 5.0, 10, 500);
            Assert.True(v > 0);
            Assert.True(v <= int.MaxValue);
        }

        [Fact]
        public void InvalidInputs_AreClamped()
        {
            Assert.Equal(1L, ProgressCurve.RequiredKills(GrowthMode.Exponential, 0, 0.5, -3, 0));
            Assert.Equal(1L, ProgressCurve.RequiredKills(GrowthMode.Linear, -5, 1.2, -3, 10));
        }
    }
}
```

- [ ] **Step 3: 运行确认失败**

Run: `cd 77-KillingReward/Tests/unit && dotnet test`
Expected: 编译失败（`ProgressCurve` 不存在）。

- [ ] **Step 4: 实现 ProgressCurve**

```csharp
using System;

namespace KillingReward.Core
{
    public static class ProgressCurve
    {
        public static long RequiredKills(GrowthMode mode, int initial, double factor, int increment, long completedLevels)
        {
            if (initial < 1) initial = 1;
            if (completedLevels < 0) completedLevels = 0;
            double value;
            if (mode == GrowthMode.Exponential)
            {
                if (factor < 1.0) factor = 1.0;
                value = initial * Math.Pow(factor, completedLevels);
            }
            else
            {
                if (increment < 0) increment = 0;
                value = initial + (double)increment * completedLevels;
            }
            long rounded = (long)Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded < 1) rounded = 1;
            if (rounded > int.MaxValue) rounded = int.MaxValue;
            return rounded;
        }
    }
}
```

- [ ] **Step 5: 运行确认通过**

Run: `cd 77-KillingReward/Tests/unit && dotnet test`
Expected: 5 个测试全部 Passed。

- [ ] **Step 6: Commit**

```bash
git add 77-KillingReward
git commit -m "Add ProgressCurve with xunit tests"
```

---

### Task 3: ProgressState 进度状态机 + 单元测试

**Files:**
- Create: `77-KillingReward/1.6/Source/Core/ProgressState.cs`
- Create: `77-KillingReward/Tests/unit/ProgressStateTests.cs`

**Interfaces:**
- Consumes: `ProgressCurve.RequiredKills`（Task 2）。
- Produces: `KillingReward.Core.ProgressState`（readonly struct）：属性 `long Level`、`long Progress`、`int Pending`；方法 `ProgressState AddKill(Func<long,long> requiredForLevel)`（不可变，返回新状态）。Task 6 的 `KillRewardTracker` 依赖此签名。

- [ ] **Step 1: 写失败测试**

```csharp
using KillingReward.Core;
using Xunit;

namespace KillingReward.UnitTests
{
    public class ProgressStateTests
    {
        private static long ConstReq(long level) => 10;

        [Fact]
        public void SingleKill_BelowRequirement_NoLevelUp()
        {
            ProgressState s = new ProgressState(0, 0, 0).AddKill(ConstReq);
            Assert.Equal(0, s.Level);
            Assert.Equal(1, s.Progress);
            Assert.Equal(0, s.Pending);
        }

        [Fact]
        public void ExactFill_LevelsUpAndCarriesZero()
        {
            ProgressState s = new ProgressState(0, 9, 0).AddKill(ConstReq);
            Assert.Equal(1, s.Level);
            Assert.Equal(0, s.Progress);
            Assert.Equal(1, s.Pending);
        }

        [Fact]
        public void Rollover_KeepsExcessProgress()
        {
            // 需求 10，已有 9，一次 +1 后仍按循环处理（此处模拟多杀连发用累加）
            ProgressState s = new ProgressState(0, 9, 0);
            for (int i = 0; i < 3; i++) s = s.AddKill(ConstReq); // 9+3=12 -> level1, progress 2
            Assert.Equal(1, s.Level);
            Assert.Equal(2, s.Progress);
            Assert.Equal(1, s.Pending);
        }

        [Fact]
        public void GrowingRequirement_MultiLevelUp()
        {
            // 需求依次为 2、3：4 杀 -> 第 1 级用 2（余 2），第 2 级需 3（余 2 不够），level=1 progress=2
            ProgressState s = new ProgressState(0, 0, 0);
            for (int i = 0; i < 4; i++) s = s.AddKill(l => l == 0 ? 2 : 3);
            Assert.Equal(1, s.Level);
            Assert.Equal(2, s.Progress);
            Assert.Equal(1, s.Pending);
        }

        [Fact]
        public void PendingAccumulates()
        {
            ProgressState s = new ProgressState(0, 0, 5).AddKill(l => 1);
            Assert.Equal(1, s.Level);
            Assert.Equal(6, s.Pending);
        }
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `cd 77-KillingReward/Tests/unit && dotnet test`
Expected: 编译失败（`ProgressState` 不存在）。

- [ ] **Step 3: 实现 ProgressState**

```csharp
using System;

namespace KillingReward.Core
{
    public readonly struct ProgressState
    {
        public readonly long Level;
        public readonly long Progress;
        public readonly int Pending;

        public ProgressState(long level, long progress, int pending)
        {
            Level = level;
            Progress = progress;
            Pending = pending;
        }

        public ProgressState AddKill(Func<long, long> requiredForLevel)
        {
            long level = Level;
            long progress = Progress + 1;
            int pending = Pending;
            long required = requiredForLevel(level);
            while (required > 0 && progress >= required)
            {
                progress -= required;
                level++;
                pending++;
                required = requiredForLevel(level);
            }
            return new ProgressState(level, progress, pending);
        }
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `cd 77-KillingReward/Tests/unit && dotnet test`
Expected: 累计 10 个测试全部 Passed。

- [ ] **Step 5: Commit**

```bash
git add 77-KillingReward
git commit -m "Add ProgressState rollover machine with tests"
```

---

### Task 4: KillEligibility / SkillMath / StackMath + 单元测试

**Files:**
- Create: `77-KillingReward/1.6/Source/Core/KillEligibility.cs`
- Create: `77-KillingReward/1.6/Source/Core/SkillMath.cs`
- Create: `77-KillingReward/1.6/Source/Core/StackMath.cs`
- Create: `77-KillingReward/Tests/unit/KillEligibilityTests.cs`
- Create: `77-KillingReward/Tests/unit/SkillMathTests.cs`
- Create: `77-KillingReward/Tests/unit/StackMathTests.cs`

**Interfaces:**
- Produces:
  - `KillEligibility.ShouldCount(bool victimHasFaction, bool victimHostileToPlayer, bool instigatorIsPlayerPawn) -> bool`
  - `SkillMath.ClampedAdd(int current, int delta, int max) -> int`
  - `StackMath.FullStackCount(int stackLimit) -> int`

- [ ] **Step 1: 写失败测试**

```csharp
using KillingReward.Core;
using Xunit;

namespace KillingReward.UnitTests
{
    public class KillEligibilityTests
    {
        [Theory]
        [InlineData(true, true, true, true)]    // 敌对派系单位被我方小人亲手击杀
        [InlineData(true, true, false, false)]  // 敌方互殴(斗蛐蛐)
        [InlineData(true, false, true, false)]  // 非敌对派系
        [InlineData(false, false, true, false)] // 无派系发狂动物
        [InlineData(false, false, false, false)]// 天灾/落石
        [InlineData(true, false, false, false)]
        [InlineData(false, true, true, false)]
        [InlineData(false, true, false, false)]
        public void TruthTable(bool hasFaction, bool hostile, bool playerPawn, bool expected)
        {
            Assert.Equal(expected, KillEligibility.ShouldCount(hasFaction, hostile, playerPawn));
        }
    }

    public class SkillMathTests
    {
        [Fact]
        public void Add3_Normal()
        {
            Assert.Equal(13, SkillMath.ClampedAdd(10, 3, 20));
        }

        [Fact]
        public void Add3_ClampsAtMax()
        {
            Assert.Equal(20, SkillMath.ClampedAdd(18, 3, 20));
            Assert.Equal(20, SkillMath.ClampedAdd(20, 3, 20));
        }

        [Fact]
        public void NeverBelowZero()
        {
            Assert.Equal(0, SkillMath.ClampedAdd(0, -5, 20));
        }
    }

    public class StackMathTests
    {
        [Fact]
        public void FullStack_IsStackLimit()
        {
            Assert.Equal(75, StackMath.FullStackCount(75));
            Assert.Equal(1, StackMath.FullStackCount(1));
        }

        [Fact]
        public void ZeroOrNegative_BecomesOne()
        {
            Assert.Equal(1, StackMath.FullStackCount(0));
        }
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `cd 77-KillingReward/Tests/unit && dotnet test`
Expected: 编译失败（三个类不存在）。

- [ ] **Step 3: 实现三个纯逻辑类**

```csharp
// Core/KillEligibility.cs
namespace KillingReward.Core
{
    public static class KillEligibility
    {
        public static bool ShouldCount(bool victimHasFaction, bool victimHostileToPlayer, bool instigatorIsPlayerPawn)
        {
            return victimHasFaction && victimHostileToPlayer && instigatorIsPlayerPawn;
        }
    }
}

// Core/SkillMath.cs
using System;
namespace KillingReward.Core
{
    public static class SkillMath
    {
        public static int ClampedAdd(int current, int delta, int max)
        {
            return Math.Max(0, Math.Min(current + delta, max));
        }
    }
}

// Core/StackMath.cs
using System;
namespace KillingReward.Core
{
    public static class StackMath
    {
        public static int FullStackCount(int stackLimit)
        {
            return Math.Max(1, stackLimit);
        }
    }
}
```

- [ ] **Step 4: 运行确认通过**

Run: `cd 77-KillingReward/Tests/unit && dotnet test`
Expected: 累计 19 个测试全部 Passed。

- [ ] **Step 5: Commit**

```bash
git add 77-KillingReward
git commit -m "Add kill eligibility, skill and stack helpers with tests"
```

---

### Task 5: 游戏骨架（主按钮 + 信件 + 窗口骨架 + 双语翻译）

**Files:**
- Create: `77-KillingReward/1.6/Source/KillingRewardDefOf.cs`
- Create: `77-KillingReward/1.6/Source/UI/ChoiceLetter_KillingReward.cs`
- Create: `77-KillingReward/1.6/Source/UI/MainButtonWorker_KillingReward.cs`
- Create: `77-KillingReward/1.6/Source/UI/Dialog_KillingReward.cs`
- Create: `77-KillingReward/1.6/Source/RewardNotifier.cs`
- Create: `77-KillingReward/1.6/Defs/MainButtonDefs/KillingRewardMainButton.xml`
- Create: `77-KillingReward/1.6/Defs/LetterDefs/KillingRewardLetter.xml`
- Create: `77-KillingReward/Languages/English/Keyed/Keys.xml`
- Create: `77-KillingReward/Languages/ChineseSimplified/Keyed/Keys.xml`
- Create: `77-KillingReward/Languages/ChineseSimplified/DefInjected/MainButtonDef/KillingRewardMainButton.xml`

**Interfaces:**
- Produces: `RewardNotifier.NotifyLevelUp()`（Task 6 的 Tracker 调用）；`Dialog_KillingReward : Window`（Task 7-10 逐步填充）；`KillingRewardDefOf.BoonLetter`（`LetterDef`）。

- [ ] **Step 1: 写两个 Keys.xml（key 全集见 Global Constraints 表格；英文文件填英文列，中文文件填中文列）**

`77-KillingReward/Languages/English/Keyed/Keys.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<LanguageData>
	<KR_LetterTitle>Blood Boon</KR_LetterTitle>
	<KR_LetterText>The dark archotech watches every kill, and your struggle amuses it greatly. This boon is yours. Claim it — and try to stay alive.</KR_LetterText>
	<KR_LetterOpen>Accept the Boon</KR_LetterOpen>
	<KR_WindowTitle>Boon of the Dark Archotech</KR_WindowTitle>
	<KR_Progress>Blood Tithe</KR_Progress>
	<KR_Tier>Boon Tier</KR_Tier>
	<KR_Pending>Unclaimed Boons</KR_Pending>
	<KR_RewardResearch>Forbidden Knowledge</KR_RewardResearch>
	<KR_RewardResearchDesc>It sears knowledge straight into your scholars' minds. Instantly complete one available research project.</KR_RewardResearchDesc>
	<KR_RewardSkill>Bestowed Prowess</KR_RewardSkill>
	<KR_RewardSkillDesc>It plucks the strings of nerve and muscle. Choose a pawn; one of their skills gains 3 levels.</KR_RewardSkillDesc>
	<KR_RewardItem>Gift from the Void</KR_RewardItem>
	<KR_RewardItemDesc>It casts provisions from the void. Choose an item and a drop cell to receive a full stack.</KR_RewardItemDesc>
	<KR_Claimed>The boon is granted. It is still watching. Keep it entertained.</KR_Claimed>
	<KR_NoPending>It has no boon for you yet. Slaughter is prayer.</KR_NoPending>
	<KR_SettingInitial>Initial kills required</KR_SettingInitial>
	<KR_SettingInitialDesc>Kills required for the first reward tier.</KR_SettingInitialDesc>
	<KR_SettingMode>Growth mode</KR_SettingMode>
	<KR_SettingModeExponential>Exponential (× factor per tier)</KR_SettingModeExponential>
	<KR_SettingModeLinear>Linear (+ increment per tier)</KR_SettingModeLinear>
	<KR_SettingFactor>Exponential factor</KR_SettingFactor>
	<KR_SettingFactorDesc>In exponential mode, each tier costs this factor times the previous one.</KR_SettingFactorDesc>
	<KR_SettingIncrement>Linear increment</KR_SettingIncrement>
	<KR_SettingIncrementDesc>In linear mode, each tier costs this many more kills than the previous one.</KR_SettingIncrementDesc>
	<KR_PickProject>Choose a research to complete</KR_PickProject>
	<KR_PickPawn>Choose a pawn</KR_PickPawn>
	<KR_PickSkill>Choose a skill</KR_PickSkill>
	<KR_PickItem>Choose an item</KR_PickItem>
	<KR_PickCell>Click a cell to deliver the goods</KR_PickCell>
	<KR_Back>Back</KR_Back>
	<KR_ItemDelivered>The gift from the void has arrived.</KR_ItemDelivered>
	<KR_Offering>Offering +1</KR_Offering>
</LanguageData>
```

`77-KillingReward/Languages/ChineseSimplified/Keyed/Keys.xml`：结构相同，value 换成 Global Constraints 表格的中文列（如 `<KR_LetterTitle>嗜血恩赐</KR_LetterTitle>`、`<KR_WindowTitle>黑暗超凡智能的恩赐</KR_WindowTitle>` 等，逐字采用设计文档「文案与基调」）。

- [ ] **Step 2: 写 Defs**

`77-KillingReward/1.6/Defs/MainButtonDefs/KillingRewardMainButton.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
	<MainButtonDef>
		<defName>KillingReward</defName>
		<label>Killing Reward</label>
		<description>Claim boons from the dark archotech.</description>
		<workerClass>KillingReward.MainButtonWorker_KillingReward</workerClass>
		<iconPath>UI/Icons/KillingReward</iconPath>
		<order>55</order>
		<validWithoutMap>true</validWithoutMap>
	</MainButtonDef>
</Defs>
```

`77-KillingReward/1.6/Defs/LetterDefs/KillingRewardLetter.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
	<LetterDef>
		<defName>KillingRewardBoon</defName>
		<letterClass>KillingReward.ChoiceLetter_KillingReward</letterClass>
		<icon>UI/Icons/KillingReward</icon>
	</LetterDef>
</Defs>
```

`77-KillingReward/Languages/ChineseSimplified/DefInjected/MainButtonDef/KillingRewardMainButton.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<LanguageData>
	<KillingReward.label>嗜血恩赐</KillingReward.label>
	<KillingReward.description>领取黑暗超凡智能的恩赐。</KillingReward.description>
</LanguageData>
```

- [ ] **Step 3: 写 C# 骨架类**

`1.6/Source/KillingRewardDefOf.cs`：

```csharp
using RimWorld;
using Verse;

namespace KillingReward
{
    [DefOf]
    public static class KillingRewardDefOf
    {
        public static LetterDef KillingRewardBoon;

        static KillingRewardDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(KillingRewardDefOf));
        }
    }
}
```

`1.6/Source/UI/MainButtonWorker_KillingReward.cs`：

```csharp
using RimWorld;
using Verse;

namespace KillingReward
{
    public class MainButtonWorker_KillingReward : MainButtonWorker
    {
        public override void Activate()
        {
            Find.WindowStack.Add(new Dialog_KillingReward());
        }
    }
}
```

`1.6/Source/UI/ChoiceLetter_KillingReward.cs`：

```csharp
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace KillingReward
{
    public class ChoiceLetter_KillingReward : ChoiceLetter
    {
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return new DiaOption("KR_LetterOpen".Translate())
                {
                    action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_KillingReward());
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true
                };
                yield return Option_Close;
            }
        }
    }
}
```

`1.6/Source/RewardNotifier.cs`：

```csharp
using RimWorld;
using Verse;

namespace KillingReward
{
    public static class RewardNotifier
    {
        public static void NotifyLevelUp()
        {
            ChoiceLetter letter = LetterMaker.MakeLetter(
                "KR_LetterTitle".Translate(),
                "KR_LetterText".Translate(),
                KillingRewardDefOf.KillingRewardBoon);
            Find.LetterStack.ReceiveLetter(letter);
        }
    }
}
```

`1.6/Source/UI/Dialog_KillingReward.cs`（骨架；Task 7-9 加奖励视图，Task 10 定稿布局）：

```csharp
using RimWorld;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class Dialog_KillingReward : Window
    {
        public Dialog_KillingReward()
        {
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            KillRewardTracker tracker = KillRewardTracker.Instance;
            if (tracker == null)
            {
                return;
            }
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            Text.Font = GameFont.Medium;
            listing.Label("KR_WindowTitle".Translate());
            Text.Font = GameFont.Small;
            listing.Label("KR_Tier".Translate() + ": " + tracker.Level);
            listing.Label("KR_Pending".Translate() + ": " + tracker.PendingRewards);
            listing.Label("KR_Progress".Translate() + ": " + tracker.Progress + " / " + tracker.RequiredForCurrentLevel);
            if (tracker.PendingRewards <= 0)
            {
                listing.Label("KR_NoPending".Translate());
            }
            listing.End();
        }
    }
}
```

- [ ] **Step 4: 构建**

Run: `cd 77-KillingReward/1.6/Source && dotnet build -c Release`
Expected: `0 Error(s)`。注意：`Dialog_KillingReward` 引用了 Task 6 的 `KillRewardTracker`——**因此本 Step 先实现 Task 6 的 `KillRewardTracker.cs` 骨架**（只有字段/属性/ExposeData 和空的 AddKill，见 Task 6 Step 1，提前到这里创建），再构建。

- [ ] **Step 5: Commit**

```bash
git add 77-KillingReward
git commit -m "Add main button, boon letter, dialog skeleton and translations"
```

---

### Task 6: 设置界面 + KillRewardTracker + 击杀补丁

**Files:**
- Create: `77-KillingReward/1.6/Source/KillRewardTracker.cs`（Task 5 Step 4 已建骨架，本任务补全）
- Create: `77-KillingReward/1.6/Source/KillEligibilityAdapter.cs`
- Create: `77-KillingReward/1.6/Source/Patches/PawnKillPatch.cs`
- Modify: `77-KillingReward/1.6/Source/KillingRewardMod.cs`（填设置界面）

**Interfaces:**
- Consumes: `ProgressState.AddKill`（Task 3）、`KillEligibility.ShouldCount`（Task 4）、`RewardNotifier.NotifyLevelUp`（Task 5）、`KillingRewardMod.Settings`（Task 1）。
- Produces: `KillRewardTracker.Instance`、`long Level`、`long Progress`、`int PendingRewards`、`long RequiredForCurrentLevel`、`void AddKill()`、`bool TryConsumeReward()`；`KillEligibilityAdapter.ShouldCount(Pawn, DamageInfo?) -> bool`。

- [ ] **Step 1: 写 KillRewardTracker.cs（完整版）**

```csharp
using KillingReward.Core;
using RimWorld;
using Verse;

namespace KillingReward
{
    public class KillRewardTracker : GameComponent
    {
        private long level;
        private long progress;
        private int pending;

        public long Level => level;
        public long Progress => progress;
        public int PendingRewards => pending;

        public static KillRewardTracker Instance => Current.Game?.GetComponent<KillRewardTracker>();

        public KillRewardTracker(Game game)
        {
        }

        public long RequiredForCurrentLevel
        {
            get
            {
                KillingRewardSettings s = KillingRewardMod.Settings;
                return ProgressCurve.RequiredKills(s.Mode, s.InitialKills, s.ExponentialFactor, s.LinearIncrement, level);
            }
        }

        public void AddKill()
        {
            KillingRewardSettings s = KillingRewardMod.Settings;
            ProgressState before = new ProgressState(level, progress, pending);
            ProgressState after = before.AddKill(l => ProgressCurve.RequiredKills(s.Mode, s.InitialKills, s.ExponentialFactor, s.LinearIncrement, l));
            level = after.Level;
            progress = after.Progress;
            pending = after.Pending;
            if (after.Level > before.Level)
            {
                RewardNotifier.NotifyLevelUp();
            }
        }

        public bool TryConsumeReward()
        {
            if (pending <= 0)
            {
                return false;
            }
            pending--;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref level, "level", 0L);
            Scribe_Values.Look(ref progress, "progress", 0L);
            Scribe_Values.Look(ref pending, "pending", 0);
        }
    }
}
```

- [ ] **Step 2: 写 KillEligibilityAdapter.cs**

```csharp
using KillingReward.Core;
using Verse;

namespace KillingReward
{
    public static class KillEligibilityAdapter
    {
        public static bool ShouldCount(Pawn victim, DamageInfo? dinfo)
        {
            bool victimHasFaction = victim?.Faction != null;
            bool victimHostileToPlayer = victimHasFaction && victim.Faction.HostileTo(Faction.OfPlayer);
            bool instigatorIsPlayerPawn = dinfo.HasValue
                && dinfo.Value.Instigator is Pawn instigator
                && instigator.Faction != null
                && instigator.Faction.IsPlayer;
            return KillEligibility.ShouldCount(victimHasFaction, victimHostileToPlayer, instigatorIsPlayerPawn);
        }
    }
}
```

- [ ] **Step 3: 写 Patches/PawnKillPatch.cs**

```csharp
using HarmonyLib;
using UnityEngine;
using Verse;

namespace KillingReward
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class PawnKillPatch
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!KillEligibilityAdapter.ShouldCount(__instance, dinfo))
            {
                return;
            }
            KillRewardTracker.Instance?.AddKill();
            // 击杀反馈：受害者头顶红色「祭品+1」浮字（参照原版 MISS/闪避浮字机制）。
            if (__instance.Map != null)
            {
                MoteMaker.ThrowText(__instance.DrawPos + new Vector3(0f, 0f, 0.5f), __instance.Map,
                    "KR_Offering".Translate(), Color.red);
            }
        }
    }
}
```

注：`MoteMaker.ThrowText` 在 1.6 有 `(Vector3, Map, string, Color, float)` 与 `(Vector3, Map, string, float)` 等重载；若四参重载签名不符，改用 `FleckMaker.ThrowText(loc, map, text, Color.red, -1f)`，以编译为准。

- [ ] **Step 4: Harmony 初始化 + 设置界面（修改 KillingRewardMod.cs）**

```csharp
using KillingReward.Core;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace KillingReward
{
    public class KillingRewardMod : Mod
    {
        public static KillingRewardSettings Settings { get; private set; }

        public KillingRewardMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<KillingRewardSettings>();
            new Harmony("com.RunningBugs.KillingReward").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "KillingReward";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            KillingRewardSettings s = Settings;
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("KR_SettingInitial".Translate() + ": " + s.InitialKills, -1f, "KR_SettingInitialDesc".Translate());
            s.InitialKills = (int)listing.Slider(s.InitialKills, 1, 200);

            listing.Label("KR_SettingMode".Translate());
            bool exponential = s.Mode == GrowthMode.Exponential;
            if (listing.RadioButton("KR_SettingModeExponential".Translate(), exponential))
            {
                s.Mode = GrowthMode.Exponential;
            }
            if (listing.RadioButton("KR_SettingModeLinear".Translate(), !exponential))
            {
                s.Mode = GrowthMode.Linear;
            }

            if (s.Mode == GrowthMode.Exponential)
            {
                listing.Label("KR_SettingFactor".Translate() + ": " + s.ExponentialFactor.ToString("F2"), -1f, "KR_SettingFactorDesc".Translate());
                s.ExponentialFactor = listing.Slider(s.ExponentialFactor, 1.0f, 3.0f);
            }
            else
            {
                listing.Label("KR_SettingIncrement".Translate() + ": " + s.LinearIncrement, -1f, "KR_SettingIncrementDesc".Translate());
                s.LinearIncrement = (int)listing.Slider(s.LinearIncrement, 0, 200);
            }

            listing.End();
        }
    }
}
```

- [ ] **Step 5: 构建 + 单测回归**

Run: `cd 77-KillingReward/1.6/Source && dotnet build -c Release && cd ../../Tests/unit && dotnet test`
Expected: 构建 `0 Error(s)`；19 个单测全部 Passed。

- [ ] **Step 6: Commit**

```bash
git add 77-KillingReward
git commit -m "Add kill tracker, kill-counting patch and mod settings"
```

---

### Task 7: 研究奖励「禁忌知识」

**Files:**
- Create: `77-KillingReward/1.6/Source/Rewards/ResearchReward.cs`
- Modify: `77-KillingReward/1.6/Source/UI/Dialog_KillingReward.cs`（加模式切换与研究列表视图）

**Interfaces:**
- Produces: `ResearchReward.Available() -> List<ResearchProjectDef>`、`ResearchReward.Complete(ResearchProjectDef)`。Dialog 内部枚举 `View { Main, Research, SkillPawn, SkillSkill, Item }`（Task 8/9 复用该模式）。

- [ ] **Step 1: 写 Rewards/ResearchReward.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace KillingReward
{
    public static class ResearchReward
    {
        public static List<ResearchProjectDef> Available()
        {
            return DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(p => !p.IsFinished && !p.IsHidden && p.CanStartNow)
                .OrderBy(p => p.LabelCap.ToString())
                .ToList();
        }

        public static void Complete(ResearchProjectDef project)
        {
            // 原版 FinishProject 会自行写满进度、补 techprint、处理解锁与完成信件。
            Find.ResearchManager.FinishProject(project, doCompletionDialog: false, researcher: null, doCompletionLetter: true);
        }
    }
}
```

- [ ] **Step 2: Dialog 加研究视图（修改 Dialog_KillingReward.cs）**

在类中加入：

```csharp
        private enum View { Main, Research }
        private View view = View.Main;
        private Vector2 scrollPosition;

        private void DoMainView(Listing_Standard listing, KillRewardTracker tracker)
        {
            bool hasPending = tracker.PendingRewards > 0;
            using (new GUIBlock(!hasPending))
            {
                if (listing.ButtonTextLabeled("KR_RewardResearch".Translate(), "KR_PickProject".Translate()))
                {
                    view = View.Research;
                }
                listing.Label("KR_RewardResearchDesc".Translate());
            }
        }

        private void DoResearchView(Rect inRect, KillRewardTracker tracker)
        {
            List<ResearchProjectDef> projects = ResearchReward.Available();
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Widgets.Label(titleRect, "KR_PickProject".Translate());
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, projects.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (ResearchProjectDef project in projects)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(row, project.LabelCap + " (" + project.baseCost + ")"))
                {
                    if (tracker.TryConsumeReward())
                    {
                        ResearchReward.Complete(project);
                        Messages.Message("KR_Claimed".Translate(), MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            Rect backRect = new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f);
            if (Widgets.ButtonText(backRect, "KR_Back".Translate()))
            {
                view = View.Main;
            }
        }
```

并把 `DoWindowContents` 主体改为（保留 Task 5 的头部，之后按 view 分发）：

```csharp
        public override void DoWindowContents(Rect inRect)
        {
            KillRewardTracker tracker = KillRewardTracker.Instance;
            if (tracker == null)
            {
                return;
            }
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            Text.Font = GameFont.Medium;
            listing.Label("KR_WindowTitle".Translate());
            Text.Font = GameFont.Small;
            listing.Label("KR_Tier".Translate() + ": " + tracker.Level);
            listing.Label("KR_Pending".Translate() + ": " + tracker.PendingRewards);
            listing.Label("KR_Progress".Translate() + ": " + tracker.Progress + " / " + tracker.RequiredForCurrentLevel);
            listing.End();

            Rect body = new Rect(inRect.x, inRect.y + 130f, inRect.width, inRect.height - 130f);
            if (view == View.Research)
            {
                DoResearchView(body, tracker);
                return;
            }
            if (tracker.PendingRewards <= 0)
            {
                Widgets.Label(body, "KR_NoPending".Translate());
                return;
            }
            Listing_Standard main = new Listing_Standard();
            main.Begin(body);
            DoMainView(main, tracker);
            main.End();
        }
```

说明：`GUIBlock` 若在当前 RimWorld 引用集中不可用，改用 `GUI.color = new Color(1f,1f,1f, hasPending ? 1f : 0.4f)` 包裹并在点击时判断 `hasPending`——实现时以编译为准（`Verse.GUIBlock` 存在于 1.6）。

- [ ] **Step 3: 构建**

Run: `cd 77-KillingReward/1.6/Source && dotnet build -c Release`
Expected: `0 Error(s)`。

- [ ] **Step 4: Commit**

```bash
git add 77-KillingReward
git commit -m "Add research reward view (Forbidden Knowledge)"
```

---

### Task 8: 技能奖励「技艺灌注」

**Files:**
- Create: `77-KillingReward/1.6/Source/Rewards/SkillReward.cs`
- Modify: `77-KillingReward/1.6/Source/UI/Dialog_KillingReward.cs`

**Interfaces:**
- Produces: `SkillReward.Candidates() -> List<Pawn>`、`SkillReward.AvailableSkills(Pawn) -> List<SkillRecord>`、`SkillReward.Apply(SkillRecord)`（内部用 `SkillMath.ClampedAdd`，Task 4）。Dialog `View` 枚举增加 `SkillPawn`、`SkillSkill`。

- [ ] **Step 1: 写 Rewards/SkillReward.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using KillingReward.Core;
using RimWorld;
using Verse;

namespace KillingReward
{
    public static class SkillReward
    {
        private const int SkillBump = 3;

        public static List<Pawn> Candidates()
        {
            return PawnsFinder.AllMaps_FreeColonists
                .Where(p => !p.Dead && p.skills != null && p.RaceProps.Humanlike)
                .OrderBy(p => p.LabelShort)
                .ToList();
        }

        public static List<SkillRecord> AvailableSkills(Pawn pawn)
        {
            return pawn.skills.skills
                .Where(s => !s.TotallyDisabled)
                .ToList();
        }

        public static void Apply(SkillRecord skill)
        {
            // Level setter 自带 0-20 clamp；外部 Mod 的子 20 上限由它们自身机制再 clamp，本 Mod 不突破。
            skill.Level = SkillMath.ClampedAdd(skill.Level, SkillBump, SkillRecord.MaxLevel);
        }
    }
}
```

- [ ] **Step 2: Dialog 加技能视图**

枚举扩展：`private enum View { Main, Research, SkillPawn, SkillSkill }`，新增字段 `private Pawn selectedPawn;`。

`DoMainView` 中追加（紧接研究按钮之后，同一 GUIBlock 内）：

```csharp
                if (listing.ButtonTextLabeled("KR_RewardSkill".Translate(), "KR_PickPawn".Translate()))
                {
                    view = View.SkillPawn;
                }
                listing.Label("KR_RewardSkillDesc".Translate());
```

新增方法：

```csharp
        private void DoSkillPawnView(Rect inRect, KillRewardTracker tracker)
        {
            List<Pawn> pawns = SkillReward.Candidates();
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickPawn".Translate());
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, pawns.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (Pawn pawn in pawns)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(row, pawn.LabelShortCap))
                {
                    selectedPawn = pawn;
                    view = View.SkillSkill;
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.Main;
            }
        }

        private void DoSkillSkillView(Rect inRect, KillRewardTracker tracker)
        {
            List<SkillRecord> skills = SkillReward.AvailableSkills(selectedPawn);
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickSkill".Translate() + " (" + selectedPawn.LabelShortCap + ")");
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, skills.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (SkillRecord skill in skills)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                string label = skill.def.LabelCap + " " + skill.Level + " → " + SkillMath.ClampedAdd(skill.Level, 3, SkillRecord.MaxLevel);
                if (Widgets.ButtonText(row, label))
                {
                    if (tracker.TryConsumeReward())
                    {
                        SkillReward.Apply(skill);
                        Messages.Message("KR_Claimed".Translate(), MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.SkillPawn;
            }
        }
```

`DoWindowContents` 的分发逻辑扩展为：

```csharp
            if (view == View.Research)
            {
                DoResearchView(body, tracker);
                return;
            }
            if (view == View.SkillPawn)
            {
                DoSkillPawnView(body, tracker);
                return;
            }
            if (view == View.SkillSkill && selectedPawn != null)
            {
                DoSkillSkillView(body, tracker);
                return;
            }
```

文件顶部 `using` 增加 `using KillingReward.Core;` 和 `using System.Collections.Generic;`。

- [ ] **Step 3: 构建**

Run: `cd 77-KillingReward/1.6/Source && dotnet build -c Release`
Expected: `0 Error(s)`。

- [ ] **Step 4: Commit**

```bash
git add 77-KillingReward
git commit -m "Add skill reward view (Bestowed Prowess)"
```

---

### Task 9: 物品奖励「虚空馈赠」+ 玩家选格

**Files:**
- Create: `77-KillingReward/1.6/Source/Rewards/ItemReward.cs`
- Modify: `77-KillingReward/1.6/Source/UI/Dialog_KillingReward.cs`

**Interfaces:**
- Consumes: `StackMath.FullStackCount`（Task 4）。
- Produces: `ItemReward.RootCategories`（`ThingCategoryDef[]`）、`ItemReward.ThingsIn(ThingCategoryDef) -> List<ThingDef>`、`ItemReward.Deliver(ThingDef, IntVec3, Map)`。

- [ ] **Step 1: 写 Rewards/ItemReward.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using KillingReward.Core;
using RimWorld;
using Verse;

namespace KillingReward
{
    public static class ItemReward
    {
        public static readonly ThingCategoryDef[] RootCategories =
        {
            ThingCategoryDefOf.Manufactured,
            ThingCategoryDefOf.ResourcesRaw,
            ThingCategoryDefOf.Items
        };

        public static List<ThingDef> ThingsIn(ThingCategoryDef category)
        {
            List<ThingDef> result = new List<ThingDef>();
            Collect(category, result);
            return result
                .Where(d => d.PlayerAcquirable && !d.IsCorpse && !d.isUnfinishedThing && d.stackLimit > 0)
                .OrderBy(d => d.LabelCap.ToString())
                .ToList();
        }

        private static void Collect(ThingCategoryDef category, List<ThingDef> into)
        {
            into.AddRange(category.childThingDefs);
            foreach (ThingCategoryDef child in category.childCategories)
            {
                Collect(child, into);
            }
        }

        public static void Deliver(ThingDef def, IntVec3 cell, Map map)
        {
            ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            Thing thing = ThingMaker.MakeThing(def, stuff);
            thing.stackCount = StackMath.FullStackCount(def.stackLimit);
            GenSpawn.Spawn(thing, cell, map);
            Messages.Message("KR_ItemDelivered".Translate(), new LookTargets(thing), MessageTypeDefOf.PositiveEvent);
        }
    }
}
```

- [ ] **Step 2: Dialog 加物品视图与选格**

枚举扩展：`private enum View { Main, Research, SkillPawn, SkillSkill, ItemCategory, ItemThing }`，新增字段 `private ThingCategoryDef selectedCategory;`。

`DoMainView` 中追加：

```csharp
                if (listing.ButtonTextLabeled("KR_RewardItem".Translate(), "KR_PickItem".Translate()))
                {
                    view = View.ItemCategory;
                }
                listing.Label("KR_RewardItemDesc".Translate());
```

新增方法：

```csharp
        private void DoItemCategoryView(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickItem".Translate());
            float y = inRect.y + 40f;
            foreach (ThingCategoryDef category in ItemReward.RootCategories)
            {
                if (Widgets.ButtonText(new Rect(inRect.x, y, inRect.width, 32f), category.LabelCap))
                {
                    selectedCategory = category;
                    view = View.ItemThing;
                }
                y += 36f;
            }
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.Main;
            }
        }

        private void DoItemThingView(Rect inRect, KillRewardTracker tracker)
        {
            List<ThingDef> things = ItemReward.ThingsIn(selectedCategory);
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KR_PickItem".Translate() + " (" + selectedCategory.LabelCap + ")");
            Rect listRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, things.Count * 32f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (ThingDef thingDef in things)
            {
                Rect row = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(row, thingDef.LabelCap + " ×" + StackMath.FullStackCount(thingDef.stackLimit)))
                {
                    BeginItemTargeting(thingDef, tracker);
                }
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 120f, 32f), "KR_Back".Translate()))
            {
                view = View.ItemCategory;
            }
        }

        private void BeginItemTargeting(ThingDef thingDef, KillRewardTracker tracker)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            Close();
            TargetingParameters parameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetSelf = false,
                validator = ti => ti.Cell.InBounds(map) && ti.Cell.Walkable(map) && !ti.Cell.Fogged(map)
            };
            Find.Targeter.BeginTargeting(parameters, delegate(LocalTargetInfo target)
            {
                if (tracker.TryConsumeReward())
                {
                    ItemReward.Deliver(thingDef, target.Cell, map);
                    Messages.Message("KR_Claimed".Translate(), MessageTypeDefOf.PositiveEvent);
                }
            }, null, null, null, "KR_PickCell".Translate());
        }
```

`DoWindowContents` 分发逻辑再扩展：

```csharp
            if (view == View.ItemCategory)
            {
                DoItemCategoryView(body);
                return;
            }
            if (view == View.ItemThing && selectedCategory != null)
            {
                DoItemThingView(body, tracker);
                return;
            }
```

- [ ] **Step 3: 构建**

Run: `cd 77-KillingReward/1.6/Source && dotnet build -c Release`
Expected: `0 Error(s)`。若 `canTargetLocations` 在 1.6 引用集中不存在，改用 `canTargetCells = true`（以编译器提示为准，二选一）。

- [ ] **Step 4: Commit**

```bash
git add 77-KillingReward
git commit -m "Add item reward with player-picked drop cell (Gift from the Void)"
```

---

### Task 10: UI mockup（rimworld-imgui-sim）+ 布局定稿 + 主按钮图标

**Files:**
- Create: `77-KillingReward/docs/mockup/render_dialog.py`
- Create: `77-KillingReward/docs/mockup/dialog.png`（生成物）
- Create: `77-KillingReward/tools/make_icon.py`
- Create: `77-KillingReward/Textures/UI/Icons/KillingReward.png`（生成物，被 MainButtonDef/LetterDef 引用）
- Modify: `77-KillingReward/1.6/Source/UI/Dialog_KillingReward.cs`（按 mockup 定稿布局）

**Interfaces:**
- Consumes: rimworld-imgui-sim 包（`~/mine/workspace/rimworld/rimworld-imgui-sim`，README 示例展示 `IMGUIContext(w,h)`、`ctx.fillable_bar(rect, frac, fill, bg)`、`ctx.label(rect, text)`、`ctx.font/anchor/gui_color`、`ctx.solid_tex(color)`、`ctx.save(path)`）。

- [ ] **Step 1: 准备 sim 环境（用户已明确指定使用该工具）**

```bash
cd ~/mine/workspace/rimworld/rimworld-imgui-sim
python3 -m venv .venv
.venv/bin/pip install -e .[test]
```

Expected: 安装成功（README 称资产已内置）。若已存在 `.venv` 则跳过安装。

- [ ] **Step 2: 写 mockup 渲染脚本 `77-KillingReward/docs/mockup/render_dialog.py`**

设计稿：640×480 窗口。顶部标题（Medium 字号居中）；其下「恩赐等阶 / 待领取」一行；血祭进度条（黑边、暗底、暗红填充）；下方三张奖励卡片竖排：每张卡片 = 深色底块 + 标题 + 描述 + 右侧「领取」按钮。

```python
#!/usr/bin/env python3
"""Render the Dialog_KillingReward mockup with rimworld-imgui-sim."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path.home() / "mine/workspace/rimworld/rimworld-imgui-sim"))
from rimworld_imgui import IMGUIContext, Rect, Color, GameFont, TextAnchor

OUT = Path(__file__).with_name("dialog.png")

ctx = IMGUIContext(640, 480)
DARK_RED = Color(0.45, 0.08, 0.08)
CARD_BG = Color(0.14, 0.14, 0.16)
BAR_BG = Color(0.06, 0.06, 0.07)

# 标题
ctx.font = GameFont.MEDIUM
ctx.anchor = TextAnchor.UPPER_CENTER
ctx.label(Rect(0, 12, 640, 30), "黑暗超凡智能的恩赐")

# 等阶 / 待领取
ctx.font = GameFont.SMALL
ctx.anchor = TextAnchor.UPPER_LEFT
ctx.label(Rect(24, 48, 300, 22), "恩赐等阶: 2")
ctx.anchor = TextAnchor.UPPER_RIGHT
ctx.label(Rect(316, 48, 300, 22), "待领取的恩赐: 1")

# 血祭进度条
ctx.anchor = TextAnchor.MIDDLE_CENTER
ctx.fillable_bar(Rect(24, 74, 592, 22), 0.7, ctx.solid_tex(DARK_RED), ctx.solid_tex(BAR_BG))
ctx.label(Rect(24, 74, 592, 22), "血祭 7 / 10")

# 三张奖励卡片
cards = [
    ("禁忌知识", "它将知识直接烙进学者的脑海。立刻完成一项当前可研究的科技。"),
    ("技艺灌注", "它替你拨动了神经与肌肉。选择一名小人，其一项技能提升 3 级。"),
    ("虚空馈赠", "它从虚空中掷下物资。选择一种物品与投放地点，领取一整格。"),
]
y = 112
for title, desc in cards:
    ctx.draw_texture(Rect(24, y, 592, 104), ctx.solid_tex(CARD_BG))
    ctx.font = GameFont.SMALL
    ctx.anchor = TextAnchor.UPPER_LEFT
    ctx.label(Rect(36, y + 10, 400, 22), title)
    ctx.gui_color = Color(0.75, 0.75, 0.75)
    ctx.label(Rect(36, y + 34, 420, 60), desc)
    ctx.gui_color = Color(1, 1, 1)
    ctx.draw_texture(Rect(472, y + 30, 120, 44), ctx.solid_tex(Color(0.25, 0.25, 0.28)))
    ctx.anchor = TextAnchor.MIDDLE_CENTER
    ctx.label(Rect(472, y + 30, 120, 44), "领取")
    ctx.anchor = TextAnchor.UPPER_LEFT
    y += 116

ctx.save(str(OUT))
print(f"wrote {OUT}")
```

- [ ] **Step 3: 渲染并读图检查**

```bash
~/mine/workspace/rimworld/rimworld-imgui-sim/.venv/bin/python 77-KillingReward/docs/mockup/render_dialog.py
```

Expected: 生成 `77-KillingReward/docs/mockup/dialog.png`。用 ReadMediaFile 查看；若 `ctx.draw_texture` 等调用名与包内 API 不符，执行 `python -c "import rimworld_imgui; print([m for m in dir(rimworld_imgui.IMGUIContext) if not m.startswith('_')])"` 核对后修正脚本。按读图结果微调间距/配色，直到布局顺眼（重点：标题居中、进度条可读、卡片间距均匀、按钮位置一致）。

- [ ] **Step 4: 生成主按钮图标 `77-KillingReward/tools/make_icon.py` 并运行**

```python
#!/usr/bin/env python3
"""Generate a 64x64 blood-drop icon for the main button / letter."""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "Textures" / "UI" / "Icons" / "KillingReward.png"
OUT.parent.mkdir(parents=True, exist_ok=True)

img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
# 血滴：圆 + 上方三角，暗红主体 + 高光
d.ellipse((14, 22, 50, 58), fill=(140, 16, 16, 255))
d.polygon([(32, 4), (16, 34), (48, 34)], fill=(140, 16, 16, 255))
d.ellipse((24, 34, 34, 44), fill=(220, 90, 90, 255))
img.save(OUT)
print(f"wrote {OUT}")
```

Run: `python3 77-KillingReward/tools/make_icon.py`（Pillow 不可用时用 sim 的 venv：`~/mine/workspace/rimworld/rimworld-imgui-sim/.venv/bin/python`）。
Expected: 生成 `77-KillingReward/Textures/UI/Icons/KillingReward.png`，ReadMediaFile 查看确认是血滴形状。

- [ ] **Step 5: 按 mockup 定稿 Dialog 布局**

将 `Dialog_KillingReward.DoWindowContents` 的 Main 视图改为卡片式布局（数值与 Step 2 的 mockup 一致：标题 y=12、等阶行 y=48、进度条 Rect(24,74,592,22)、卡片起 y=112 高 104 间距 12）：

```csharp
        private void DoMainCards(Rect body, KillRewardTracker tracker)
        {
            DrawRewardCard(new Rect(body.x, body.y, body.width, 104f),
                "KR_RewardResearch".Translate(), "KR_RewardResearchDesc".Translate(),
                tracker.PendingRewards > 0, () => view = View.Research);
            DrawRewardCard(new Rect(body.x, body.y + 116f, body.width, 104f),
                "KR_RewardSkill".Translate(), "KR_RewardSkillDesc".Translate(),
                tracker.PendingRewards > 0, () => view = View.SkillPawn);
            DrawRewardCard(new Rect(body.x, body.y + 232f, body.width, 104f),
                "KR_RewardItem".Translate(), "KR_RewardItemDesc".Translate(),
                tracker.PendingRewards > 0, () => view = View.ItemCategory);
        }

        private static void DrawRewardCard(Rect rect, string title, string desc, bool enabled, Action onClaim)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.14f, 0.14f, 0.16f));
            Rect titleRect = new Rect(rect.x + 12f, rect.y + 10f, 400f, 22f);
            Widgets.Label(titleRect, title);
            Rect descRect = new Rect(rect.x + 12f, rect.y + 34f, 420f, 60f);
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(descRect, desc);
            GUI.color = Color.white;
            Rect buttonRect = new Rect(rect.xMax - 144f, rect.y + 30f, 120f, 44f);
            using (new GUIBlock(!enabled))
            {
                if (Widgets.ButtonText(buttonRect, "KR_LetterOpen".Translate()))
                {
                    onClaim();
                }
            }
        }
```

头部改为：标题居中（`Text.Anchor = TextAnchor.UpperCenter` 绘制后还原）、等阶/待领取左右分列、进度条用 `Widgets.FillableBar(rect, (float)tracker.Progress / tracker.RequiredForCurrentLevel)` 并在其上居中绘制 "KR_Progress 当前 / 要求"。Main 视图时调用 `DoMainCards(body, tracker)` 替代 Task 7 的 `DoMainView`（`DoMainView` 删除，卡片回调直接切 view）。文件顶部 `using System;`。

- [ ] **Step 6: 构建**

Run: `cd 77-KillingReward/1.6/Source && dotnet build -c Release`
Expected: `0 Error(s)`。

- [ ] **Step 7: Commit**

```bash
git add 77-KillingReward
git commit -m "Polish reward dialog layout per imgui mockup; add blood-drop icon"
```

---

### Task 11: 静态白盒测试 + README + 全量验证

**Files:**
- Create: `77-KillingReward/Tests/whitebox/test_killingreward_static.py`
- Create: `77-KillingReward/Tests/run_whitebox.sh`
- Create: `77-KillingReward/README.md`

**Interfaces:**
- Consumes: 全部产物文件。

- [ ] **Step 1: 写静态白盒测试 `77-KillingReward/Tests/whitebox/test_killingreward_static.py`**

```python
#!/usr/bin/env python3
"""Static whitebox checks for the KillingReward mod (no game required)."""
import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

MOD_ROOT = Path(__file__).resolve().parents[2]
SOURCE = MOD_ROOT / "1.6" / "Source"


def keyed_keys(path: Path) -> set:
    return {child.tag for child in ET.parse(path).getroot()}


class StaticTests(unittest.TestCase):
    def test_about_metadata(self):
        root = ET.parse(MOD_ROOT / "About" / "About.xml").getroot()
        self.assertEqual(root.findtext("packageId"), "RunningBugs.KillingReward")
        self.assertIn("KillingReward", root.findtext("name"))
        self.assertIn("嗜血恩赐", root.findtext("name"))
        self.assertEqual([v.text for v in root.find("supportedVersions")], ["1.6"])
        deps = [d.findtext("packageId") for d in root.find("modDependencies")]
        self.assertIn("brrainz.harmony", deps)

    def test_translation_keys_match(self):
        en = keyed_keys(MOD_ROOT / "Languages" / "English" / "Keyed" / "Keys.xml")
        zh = keyed_keys(MOD_ROOT / "Languages" / "ChineseSimplified" / "Keyed" / "Keys.xml")
        self.assertEqual(en, zh)
        self.assertGreater(len(en), 20)

    def test_design_flavor_strings_present(self):
        zh = (MOD_ROOT / "Languages" / "ChineseSimplified" / "Keyed" / "Keys.xml").read_text(encoding="utf-8")
        for snippet in ["嗜血恩赐", "尽量别死", "黑暗超凡智能的恩赐", "血祭", "禁忌知识", "技艺灌注", "虚空馈赠", "杀戮即是祈祷", "祭品+1"]:
            self.assertIn(snippet, zh)

    def test_kill_patch_targets_pawn_kill(self):
        patch = (SOURCE / "Patches" / "PawnKillPatch.cs").read_text(encoding="utf-8")
        self.assertIn("HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))", patch)

    def test_core_logic_has_no_verse_dependency(self):
        for cs in (SOURCE / "Core").glob("*.cs"):
            text = cs.read_text(encoding="utf-8")
            self.assertNotRegex(text, r"using\s+(Verse|RimWorld|UnityEngine)", cs.name)

    def test_defs_reference_existing_worker_classes(self):
        main_button = (MOD_ROOT / "1.6" / "Defs" / "MainButtonDefs" / "KillingRewardMainButton.xml").read_text(encoding="utf-8")
        self.assertIn("KillingReward.MainButtonWorker_KillingReward", main_button)
        self.assertTrue((SOURCE / "UI" / "MainButtonWorker_KillingReward.cs").exists())
        letter = (MOD_ROOT / "1.6" / "Defs" / "LetterDefs" / "KillingRewardLetter.xml").read_text(encoding="utf-8")
        self.assertIn("KillingReward.ChoiceLetter_KillingReward", letter)
        self.assertTrue((SOURCE / "UI" / "ChoiceLetter_KillingReward.cs").exists())

    def test_icon_exists_for_main_button_and_letter(self):
        self.assertTrue((MOD_ROOT / "Textures" / "UI" / "Icons" / "KillingReward.png").exists())


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: 写 Tests/run_whitebox.sh**

```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/whitebox"
python3 -m unittest -v test_killingreward_static
```

并 `chmod +x 77-KillingReward/Tests/run_whitebox.sh`。

- [ ] **Step 3: 运行白盒测试**

Run: `bash 77-KillingReward/Tests/run_whitebox.sh`
Expected: 全部 `ok`，`OK`。

- [ ] **Step 4: 写 README.md（中英双语简介）**

```markdown
# KillingReward 嗜血恩赐

我方小人亲手击杀敌对派系单位可积累「血祭」进度。进度打满即可获得黑暗超凡智能的恩赐（三选一）：

- **禁忌知识**：立刻完成一项当前可研究的科技
- **技艺灌注**：一名小人的一项技能 +3 级
- **虚空馈赠**：任选一种物品，在指定格子领取一整格

通过底部主按钮栏「嗜血恩赐」随时打开奖励窗口；每次升级会收到信件提醒。
初始要求 10 杀，之后每级 ×1.2（可在 Mod 设置中改为线性增长或调整参数）。

Kills by your own colonists against hostile factions fill a blood tithe. Each filled tithe earns a boon from the dark archotech: instantly complete a research, raise a pawn's skill by 3, or receive a full stack of an item of your choice at a cell you pick. Open the reward window anytime from the "Killing Reward" main button. Defaults: first tier 10 kills, ×1.2 per tier (linear mode and all parameters configurable in mod settings).

## 设计文档 / Docs

- [设计文档](docs/2026-07-31-KillingReward-design.md)
- [实现计划](docs/2026-07-31-KillingReward-implementation-plan.md)

## 测试 / Tests

```bash
cd Tests/unit && dotnet test   # C# 单元测试
bash Tests/run_whitebox.sh     # 静态白盒检查
```
```

- [ ] **Step 5: 全量验证**

```bash
cd 77-KillingReward/1.6/Source && dotnet build -c Release
cd ../../Tests/unit && dotnet test
cd .. && bash run_whitebox.sh
```

Expected: 构建 0 Error；19 个 C# 单测全过；7 个静态白盒测试全过。

- [ ] **Step 6: Commit**

```bash
git add 77-KillingReward
git commit -m "Add whitebox tests and README for KillingReward"
```

---

## Self-Review 记录

- **Spec coverage**：击杀计数（Task 6）✓；击杀浮字反馈「祭品+1」（Task 6 Step 3）✓；进度曲线+设置（Task 2/6）✓；升级信件+主按钮（Task 5）✓；研究/技能/物品奖励（Task 7/8/9）✓；选格投放（Task 9）✓；双语（Task 5 + DefInjected）✓；imgui-sim mockup（Task 10）✓；单元+白盒测试（Task 2-4、11）✓；软链接部署（Task 1）✓；事件类奖励不做 ✓。
- **类型一致性**：`ProgressState.AddKill(Func<long,long>)` ↔ Tracker 调用一致；`SkillMath.ClampedAdd` 三处签名一致；`tracker.TryConsumeReward()` 三个奖励一致；`RewardNotifier.NotifyLevelUp` 定义（Task 5）先于使用（Task 6）。
- **已知执行期注意点**：`canTargetLocations` vs `canTargetCells`（Task 9 Step 3 已注明以编译为准）；`GUIBlock` 不可用时用 `GUI.color` 替代（Task 7 Step 2 已注明）；Task 5 Step 4 要求先建 Tracker 骨架再构建（已在步骤内注明）。
