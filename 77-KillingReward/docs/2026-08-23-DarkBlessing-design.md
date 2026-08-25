# 黑暗赐福（Dark Blessing）设计

2026-08-23 追加。第四种奖励：面向全体殖民地成员的群体增益。内部分多个类别，首个类别为「生命回复（3 天）」。

## 目标 Buff 调研结论

目标效果即 Anomaly DLC 的 `Regeneration`（再生）hediff（食尸鬼、血肉兽自带）：

- 定义：`Data/Anomaly/Defs/HediffDefs/Hediffs_Global_Misc.xml`，阶段按 severity 分档（100 / 350）
- 机制：`Pawn_HealthTracker.cs`（约 1168 行起），每 15 tick 结算一次；`<regeneration>` 数值 × 1 = 每天回复 HP
- 结算顺序：先平摊治疗全部 `Hediff_Injury`；点数有剩余时长回一个缺失部位（父部位须无伤、未缺失、无假体），新部位以约 90% 重伤状态长出，随后继续养满

### 原版机制的两个门槛（决定自包含实现的原因）

1. `RaceProps.IsFlesh || IsAnomalyEntity` —— 机械体不生效，要打 patch 才能覆盖机械体奴隶
2. `ModsConfig.AnomalyActive` —— 玩家未启用 Anomaly DLC 时整套结算不运行

### 方案对比

| | 用原版 `regeneration` stat | 自包含复刻（采用） |
| --- | --- | --- |
| Anomaly DLC | 必须启用 | 无依赖 |
| 机械体 | 需 patch 门槛 | 一视同仁（mech 的伤/缺件同样是 `Hediff_Injury` / `Hediff_MissingPart`） |
| Harmony patch | 需要 | 零 patch |
| 数值/时长控制 | 受制原版结构 | 完全自主 |

## 实现

- `1.6/Defs/HediffDefs/KRBlessingHediffs.xml` — `KillingReward_BlessingRegen`（生命回复）：
  - `HediffCompProperties_Disappears`：`180000~180000`（3 天）+ `showRemainingTime`，健康面板显示倒计时
  - `KillingReward.HediffCompProperties_KRRegeneration`：`healPerDay 350`（与原版 severity ≥0.5 档的再生速率相同）
- `1.6/Source/Hediffs/HediffComp_KRRegeneration.cs` — 在 `CompPostTickInterval` 内按 `IsHashIntervalTick(15, delta)` 复刻原版结算，去掉上述两个门槛
- `1.6/Source/Rewards/BlessingReward.cs` — 赐福类别表（`BlessingOption`，后续类别在此追加）与应用逻辑：
  - 目标集合：`PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction`（殖民者、奴隶、动物、机械体，含远征队成员）
  - 重复领取同一赐福：刷新 `HediffComp_Disappears` 剩余时间，不叠加
- UI：奖励窗口新增「黑暗赐福」卡片与子视图（窗口高度 480 → 600）
- 数值：350 HP/天 × 3 天 = 1050 治疗量，治完伤后空闲点数持续长肢体（每 15 tick 最多 1 个部位）

## 翻译与文档

- Keyed（中英）：`KR_RewardBlessing*`、`KR_BlessingRegen*`、`KR_PickBlessing`、`KR_BlessingApplied`
- DefInjected（简中）：`KillingReward_BlessingRegen` 的 label/description
- About.xml、README.md 同步更新

## 特质修改（2026-08-23 追加）

- 入口：黑暗赐福 → 特质修改 → 选小人（玩家派系人类，含奴隶，含远征队）→ 添加 / 移除
- 添加列表：全量 `TraitDef` × degreeDatas 逐程度展开，排除已持有及与现有特质冲突（`TraitDef.ConflictsWith`）；多程度特质显示为「特质名：程度名」
- 实现：`TraitSet.GainTrait` / `RemoveTrait`（内部自动处理工作类型、技能、想法、能力、需求联动）
- **点数消耗**：消耗待领取恩赐，初始 1 点，每次修改后下次更贵；增长曲线与血祭升级共用 `ProgressCurve`（指数/线性，四舍五入），参数在 Mod 设置「特质修改点数消耗」中配置（默认指数 ×1.2，线性 +1）；计数全局共享并随存档持久化（`KillRewardTracker.traitMods`）

## 基因修改（2026-08-23 追加）

- 入口：黑暗赐福 → 基因修改 → 选小人 → 系谱基因 / 异种基因 → 添加 / 移除 → 选择基因
- **无 Biotech DLC 时选项不显示**（`ModsConfig.BiotechActive` 门控）；基因类均在主程序集内，Mod 对 DLC 零依赖
- 添加列表：全量 `GeneDef`，排除已持有（无论系谱/异种）；带图标与代谢/复杂度数值；**搜索框**按名称或 defName 过滤（忽略大小写，支持中英文）
- 移除列表：按所选类型列出现有基因，同样支持搜索
- 实现：`Pawn_GeneTracker.AddGene(def, xenogene)` / `RemoveGene(gene)`
- 点数消耗：同特质修改（独立计数 `geneMods`、独立设置区「基因修改点数消耗」）
- 界面模拟：`docs/mockup/render_gene_picker.py` → `gene_picker.png`（rimworld-imgui-sim）
