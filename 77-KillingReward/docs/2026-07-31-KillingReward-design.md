# KillingReward 嗜血恩赐 — 设计文档

日期：2026-07-31
状态：已获用户批准（2026-07-31）

## 概述

环世界由战斗和运营两部分构成，运营部分较枯燥。本 Mod 让战斗产出替代运营功能：
击杀敌对派系单位累积进度，进度打满后可领取奖励（三选一）：立刻完成一项当前可研究的科技、
为一名小人的某项技能 +3 级、或领取一整格指定物品（由玩家指定投放格子）。

奖励体系设计为可扩展结构，未来可增加事件类奖励（如全图心灵抚慰），本期不实现。

## 基本信息

- 目录：`77-KillingReward`（仓库根目录下，沿用编号约定）
- packageId：`RunningBugs.KillingReward`
- 名称：KillingReward 嗜血恩赐
- 支持版本：仅 1.6
- 依赖：Harmony
- 语言：英文 + 简体中文双语，全部文本走 `Languages/{English,ChineseSimplified}/Keyed` 翻译，代码内不写死文案
- 所有 Mod 文档（本设计文档、README 等）均放在 Mod 目录内

## 击杀计数

- Harmony postfix patch `Pawn.Kill(DamageInfo?, Hediff)`。
- 判定逻辑抽成纯函数：

  ```csharp
  // 纯函数，可直接单元测试，不依赖 Verse 类型
  KillEligibility.ShouldCount(bool victimHasFaction, bool victimHostileToPlayer, bool instigatorIsPlayerPawn)
  ```

  外加薄 Verse 适配层从 `Pawn victim` 与 `DamageInfo? dinfo` 提取三个布尔值。
- 计数规则（三个条件同时满足）：
  1. 受害者有派系（`victim.Faction != null`）；
  2. 该派系对玩家敌对（`victim.Faction.HostileTo(Faction.OfPlayer)`，机械族/虫族等天生敌对派系计入，无派系发狂动物不计）；
  3. 击杀者是玩家派系的小人（`dinfo.Instigator is Pawn p && p.Faction == Faction.OfPlayer`）。
- 明确不计：炮塔/陷阱（Instigator 为建筑）、落石等天灾（Instigator 为空）、敌方互殴"斗蛐蛐"（Instigator 非我方）、被打倒后流血/休克而死（死亡瞬间无击杀者）。
- 任何地图上的击杀都计入（含远征伏击图）。
- 击杀反馈：判定成功时在**受害者头顶**弹出红色「祭品+1」浮字（MoteText，参照原版 MISS/闪避浮字机制；「祭品」指被献祭的敌人，故浮字锚定在敌人身上）。注意实现细节：`Pawn.Kill` 结束时 pawn 已被收入尸体（Corpse）并脱离地图，浮字必须用 `__instance.Corpse` 定位。滴血特效（血花粒子/滴血贴图/滴血动画）已评估，本期不做，后续可叠加原版 `BloodSplash` 粒子实现。
- `KillRewardTracker`（GameComponent，随存档 ExposeData 序列化）字段：
  - `level`：已达成的等级数
  - `progress`：当前进度（击杀数）
  - `pendingRewards`：待领取奖励次数
- 每记 1 杀 progress +1；当 progress ≥ 当前要求：progress 减去要求、level +1、pendingRewards +1、发送升级信件。
  一次补多级（理论上）也要正确结转。

## 进度曲线与设置

- `ProgressCurve` 纯函数类，第 n 级（n 从 0 起，表示已完成 n 次升级后的下一级）要求击杀数：
  - 指数模式：`Round(initial × factor^n)`
  - 线性模式：`initial + increment × n`
- Mod 设置（ModSettings + 设置界面）：
  - 总开关 enabled，默认开（关闭后击杀补丁直接跳过、主按钮隐藏，无需在模组列表中移除 Mod）
  - 初始击杀要求 initialKills，默认 10（整数，范围 1–1000）
  - 增长模式 growthMode：指数 / 线性，默认指数
  - 指数倍率 exponentialFactor，默认 1.2（浮点，范围 1.0–5.0）
  - 线性增量 linearIncrement，默认 10（整数，范围 0–1000）

## 通知与入口

- 新增 MainButtonDef「嗜血恩赐」（底部主按钮栏），随时打开奖励窗口。
- 升级时发送 `ChoiceLetter`（原版 Messages 不支持链接，信件是唯一能带"打开"按钮的通知形式）：
  含「打开奖励界面」选项，点击直接打开奖励窗口。待领取次数可累计，信件可叠加在信件栏稍后处理。

## 奖励窗口（Dialog_KillingReward，IMGUI）

- 顶部区域：当前等级、进度条（当前进度 / 当前要求，条上文字为红色）、待领取奖励次数。
- 三个奖励卡片（按钮），仅 pendingRewards > 0 时可用：
  1. **立刻完成研究**：列出当前可开始的研究项目（前置已满足、未隐藏、未完成，含正在进行中的当前项目），
     点选后立即完成（进度写满后调用原版 `ResearchManager.FinishProject`，保留原版完成信件/解锁逻辑）。
  2. **技能 +3**：先选殖民者（玩家派系的自由人类小人），再选技能；
     通过原版 `SkillRecord.Level` setter 加 3（setter 自带 0–20 clamp；
     外部 Mod 的子 20 上限由它们自身机制再 clamp，本 Mod 不做任何突破）。
     `TotallyDisabled`（因无能特性/背景完全禁用）的技能不出现在可选列表。
  3. **领取物品**：按 ThingCategory 类别浏览（三个顶层类别：制成品 Manufactured、原材料 ResourcesRaw、
     物品 Items，可下钻子类别）→ 选具体 ThingDef
     （可堆叠、玩家可获取；列表带物品图标，并有按名称（中英文/defName）过滤的搜索框）→ 进入选格模式，玩家点击一个有效格子 → 在该格 `GenSpawn.Spawn` 一整格
     （数量为 `def.stackLimit`）。
- 每次领取后 pendingRewards −1，可继续领取直到次数用尽。
- UI 迭代方式：先用 `~/mine/workspace/rimworld/rimworld-imgui-sim` 离线渲染 mockup PNG
  并读图检查，布局满意后再按图实现 C# IMGUI 代码。

## 文案与基调

整体基调：黑暗超凡智能把殖民地当角斗表演看，语气居高临下、带着玩味——「杀戮即献祭，活着是要求」。
以下为实际使用的双语字符串（Keyed 翻译，key 名实现时定）：

| 位置 | 中文 | English |
| --- | --- | --- |
| 升级信件标题 | 嗜血恩赐 | Blood Boon |
| 升级信件正文 | 它在黑暗中注视着每一场杀戮，你的挣扎令它非常满意。这份恩赐是你应得的。领取它——然后，尽量别死。 | The dark archotech watches every kill, and your struggle amuses it greatly. This boon is yours. Claim it — and try to stay alive. |
| 信件打开按钮 | 接受恩赐 | Accept the Boon |
| 主按钮 | 嗜血恩赐 | Killing Reward |
| 窗口标题 | 黑暗超凡智能的恩赐 | Boon of the Dark Archotech |
| 进度条 | 血祭 | Blood Tithe |
| 等级 | 恩赐等阶 | Boon Tier |
| 待领取计数 | 待领取的恩赐 | Unclaimed Boons |
| 研究卡标题 | 禁忌知识 | Forbidden Knowledge |
| 研究卡描述 | 它将知识直接烙进学者的脑海。立刻完成一项当前可研究的科技。 | It sears knowledge straight into your scholars' minds. Instantly complete one available research project. |
| 技能卡标题 | 技艺灌注 | Bestowed Prowess |
| 技能卡描述 | 它替你拨动了神经与肌肉。选择一名小人，其一项技能提升 3 级。 | It plucks the strings of nerve and muscle. Choose a pawn; one of their skills gains 3 levels. |
| 物品卡标题 | 虚空馈赠 | Gift from the Void |
| 物品卡描述 | 它从虚空中掷下物资。选择一种物品与投放地点，领取一整格。 | It casts provisions from the void. Choose an item and a drop cell to receive a full stack. |
| 领取完成消息 | 恩赐已兑现。它仍在注视——继续取悦它吧。 | The boon is granted. It is still watching. Keep it entertained. |
| 无待领取提示 | 它还没有新的恩赐。杀戮即是祈祷。 | It has no boon for you yet. Slaughter is prayer. |

## 测试

- C# 单元测试（dotnet test，独立于游戏运行）：
  - `ProgressCurve`：两种模式的取值、取整、边界（第 0 级、大等级防溢出）；
  - 进度结转：一次击杀恰好打满、跨多级；
  - `KillEligibility.ShouldCount`：三个布尔条件的真值表全覆盖；
  - 技能 +3 的 clamp 逻辑（18 → 20，而不是 21）；
  - 物品整格数量取 `stackLimit`。
  纯逻辑不依赖 Verse 类型，可脱离游戏进程运行。
- Python 静态白盒测试（沿用 55-CommonModCompatibilityPatches 的 Tests 模式）：
  - Harmony patch 的目标方法签名与游戏程序集一致；
  - About.xml 描述与源码实际内容同步；
  - 英文/中文翻译 key 一一对应、无缺失。

## 构建与部署

- csproj 沿用仓库约定：`net472` + `Krafs.Rimworld.Ref` + `Lib.Harmony`，输出到 `1.6/Assemblies`。
- 编译后将 Mod 目录软链接到 `/Data/SteamLibrary/steamapps/common/RimWorld/Mods/77-KillingReward`
  （与 01-AlertUtility、16-ResearchPrerequisites 现状一致），进游戏即可体验。

## 明确不做（本期）

- 事件类奖励（如全图心灵抚慰）——奖励选项在代码中是可扩展的注册结构，后续新增类别不改主流程。
- 不统计任何非直接击杀（炮塔、陷阱、天灾、敌方互殴）。
- 不支持 1.4/1.5 版本。
