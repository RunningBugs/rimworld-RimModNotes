# chunk-14 调研笔记（RimWorld 组：JobDriver_UseItemResearchBench ~ JobGiver_Steal）

本片 200 个文件全部是 **JobDriver / JobGiver**（AI 行为树叶子节点）。绝大多数是仪式、社交、战斗、搬运等通用行为，与海洋无关；真正的价值集中在 Odyssey 真空自救 AI、钓鱼、着装/真空服保护、火焰行为族这几处。

## TL;DR

1. **`JobGiver_FindOxygen.cs` —— 本片最重要文件，"海水窒息→逃回气密室"行为的逐行模板。** Odyssey 真空暴露自救 JobGiver：当小人 `ConcernedByVacuum`、已带 `VacuumExposure` hediff 且阶段≥1、所在格（或当前 job 目标格）vacuum≥0.5 时，BFS 遍历 Region 找最近 `Room.Vacuum < 0.5` 的房间，发 `JobDefOf.GotoOxygenatedArea`。把 Vacuum 换成"海水/溺水"就是海底 Mod 的核心自救 AI。
2. **`JobGiver_OptimizeApparel.cs` —— 真空中不脱真空服的特判，直接类比"海水中不脱潜水服"。** 第 94-97 行：若 `Biome.inVacuum` 且当前格 vacuum≥0.5，即使着装政策不允许，也不自动脱有 `VacuumResistance` 的衣服。
3. **`JobGiver_GetFood.cs` —— Odyssey 钓鱼 AI（`TryFindFishJob`），海底食物来源的直接参考。** 无食物时若 `RaceProps.canFishForFood`，从 `map.waterBodyTracker` 选未冻结、有鱼、`Population > 10` 的水体，发 `JobDefOf.FishAnimal`。
4. **`JobGiver_SeekSafeTemperature.cs` —— FindOxygen 的结构原型。** 同样的"BFS Region + 检查 `Room.Temperature` + 找允许格"模式；海底恒温 4°C 下它会驱使失温小人回暖气房，说明这套模式在新环境伤害类型上可复用。
5. **火焰行为族（ExtinguishSelf / FleeFire / FleeDanger / FleeImmediateThreat / FireStartingSpree / FightFiresNearPoint / AnimalFlee / JumpInWater）—— 全水下地图的冲突点。** 水下火不燃烧时这些 JobGiver 大多自然不触发（找不到 Fire / 可燃物即返回 null，不会报错）；但 `JobGiver_JumpInWater` 证明原版用 `terrain.extinguishesFire` 标记水域灭火行为，海底地形与此标志的交互需要设计。
6. **`JobGiver_BoardOrLeaveGravship.cs` / `JobGiver_GoToValidSubstructure.cs` —— 登船/离船 AI 模板。** 若海底 Mod 做"潜水钟/潜艇/海面电梯"类载具，`GravshipUtility.IsOnboardGravship` + `pawnsToBoard/pawnsToLeave` 列表这套流程可借用。

## 1. 真空自救 AI（强相关，核心模板）

`JobGiver_FindOxygen.cs`（134 行，全文精读）——Odyssey 中"小人发现自己在真空里快窒息了，主动跑回有氧房间"的完整逻辑：

- **触发条件链**（`TryGiveJob`）：
  1. `ModsConfig.OdysseyActive`；
  2. 玩家没有强制 job（`PawnUtility.PlayerForcedJobNowOrSoon`）；
  3. `pawn.ConcernedByVacuum`（种族/装备层面是否怕真空——机械、穿全套真空服的不怕）；
  4. 已有 `HediffDefOf.VacuumExposure` 且 `CurStageIndex >= 1`（即不是一沾真空就跑，而是暴露加重到一定程度才逃）；
  5. 当前位置或**当前 job 的目的地**（`GuessJobDestination` 依次看 targetA/B/C）vacuum ≥ 0.5——目的地在真空中也会提前逃，这个细节很贴心。
- **寻路**：`ClosestOxygenatedRegion` 用 `RegionTraverser.BreadthFirstTraverse`（上限 9999 region）从当前 Region 出发，跳过 doorway region，找到第一个 `r.Room.Vacuum < 0.5f` 且含有合法格（未战争迷雾、在允许区域内）的 Region；然后 `TryGetAllowedCellInRegion` 先随机 100 次、再全量乱序扫，取一个可用格，发 `JobMaker.MakeJob(JobDefOf.GotoOxygenatedArea, cell)`。
- **海洋类比**：把 `VacuumExposure` hediff 换成"溺水/水压暴露"、`Room.Vacuum` 换成"房间是否进水/是否气密"、`GetVacuum` 换成对应的海水查询，整套自救行为即可平移。关键 API：`IntVec3.GetVacuum(Map)`、`Room.Vacuum`、`Pawn.ConcernedByVacuum`、`JobDefOf.GotoOxygenatedArea`。

## 2. 着装与真空服（相关）

`JobGiver_OptimizeApparel.cs`（417 行，精读真空相关段）：小人每 6000-9000 tick 评估一次换装。第 90-104 行的脱衣循环里有真空特判：

```csharp
if (pawn.MapHeld.Biome.inVacuum && pawn.Position.GetVacuum(pawn.MapHeld) >= 0.5f)
    flag = apparel.def.equippedStatOffsets.GetStatOffsetFromList(StatDefOf.VacuumResistance) > 0f;
```

`flag` 为真时跳过"着装政策不允许就脱掉"的逻辑——即**在真空生物群系的真空格上，哪怕政策不允许，也绝不自动脱真空服**。

- **海洋类比**：潜水服在"海水格"上同样不应被自动脱掉。注意此保护以 `Biome.inVacuum` 为前提；海底图若不是 inVacuum 生物群系，需要自己实现等价保护（或把海底做成 inVacuum 变种）。
- 附带结论：换装评分 `ApparelScoreRaw` 只看护甲/保温/品质等，**不看 VacuumResistance**——原版不会"因为要去真空而主动穿真空服"，只保证不脱。海底 Mod 若要"出舱前主动穿潜水服"，需要自己加评分项。

## 3. 钓鱼 / 水体食物（相关）

`JobGiver_GetFood.cs` 末尾的 `TryFindFishJob`（Odyssey）：

- 触发：常规食物搜索失败、且 `ModsConfig.OdysseyActive && pawn.RaceProps.canFishForFood`。
- 数据源：`map.waterBodyTracker`（`WaterBodyTracker`），遍历 `Bodies`，过滤 `!TotallyFrozen && CommonFish 非空 && Population > 10f`。
- 选点：BFS Region 找到与某水体 `Bounds` 重叠且 region 格含水体格的区域，再筛 `Standable && GetWaterBody == 该水体 && terrain.IsWater` 的格，随机 30 次取不禁止、可预订可达的格，发 `JobDefOf.FishAnimal`。
- **海洋类比**：海底 Mod 若用原版水体系统放鱼群，这套"饿了去捕鱼"的 AI 直接可用；`WaterBody.Population` 是资源量字段（捕鱼会消耗，别处应会回补）。注意原版要求格子 `Standable`——深海格若不可站立，需要另设"潜水捕鱼"分支。

## 4. 温度安全区 AI（相关，模板/冲突双面）

`JobGiver_SeekSafeTemperature.cs`：`requiresInjury`（默认 true）时要求已有 Serious 级温度伤害才触发；若当前环境温度在舒适区且 `waitInSafeTemp`，发 `Wait_SafeTemperature`；否则 `ClosestRegionWithinTemperatureRange`——与 FindOxygen 几乎相同的 BFS（跳过 doorway、找允许格、检查 `r.Room.Temperature` 是否落在 `ComfortableTemperatureRange`），发 `JobDefOf.GotoSafeTemperature`。

- **海洋类比**：FindOxygen 明显是照这个文件改的（连 `TryGetAllowedCellInRegion` 都复制了），再次印证"新环境伤害类型 → 抄这个模式"是官方做法。
- **冲突面**：海底室外恒温 ~4°C，低于人类舒适区——任何失温小人都会被这个 JobGiver 赶着回暖气房（这是想要的行为）；但它只认 `Room.Temperature`，若海底 Mod 用别的方式表达"水下冷"，它感知不到。

## 5. 火焰行为族（冲突类）

以下文件全部围绕"火"：自我灭火、逃火、纵火、救火。海底（非气密区充满海水）环境下火理应无法存在，需要逐一确认的行为：

- `JobGiver_ExtinguishSelf.cs`：10% 概率/tick 检查自己身上有无 `ThingDefOf.Fire` 附着，有才发 `ExtinguishSelf`。水下不会着火 → 不触发，无害。
- `JobGiver_JumpInWater.cs`：身上着火时找 5~16 格内 `terrain.extinguishesFire` 的格跳进去（原版水域灭火的实现，纯地形标志驱动）。**若海底全图地形都标了 extinguishesFire，着火的 pawn 会执行一个无意义的 Goto**；反过来若海底地形不该灭火（比如气密室内仍可着火），标志要区分。
- `JobGiver_FleeFire.cs` / `JobGiver_FleeDanger.cs` / `JobGiver_FleeImmediateThreat.cs` / `JobGiver_AnimalFlee.cs`：都调 `FleeUtility.FleeLargeFireJob`（检测邻近大火才逃）。无火则返回 null，无害。
- `JobGiver_FireStartingSpree.cs`（纵火狂崩溃）：要求目标 `thing.FlammableNow && !thing.IsBurning()`；找不到可燃物就退化为 `GotoWander` 游走，**不会卡死或报错**，水下安全。
- `JobGiver_FightFiresNearPoint.cs`：救火 job，无火不触发。

结论：这一族在水下全部"静默失效"，不需要 patch；真正要管的是"气密室内可以着火、非气密区不能"这条分界线落在哪个系统（terrain/room 属性），不在本片。

## 6. 登船 AI（弱相关，可借用）

- `JobGiver_BoardOrLeaveGravship.cs`：读地图上第一个 `ThingDefOf.GravEngine`，若 pawn 在 `building_GravEngine.pawnsToBoard` / `pawnsToLeave` 列表里，用 `GravshipUtility.TryFindSpotOnGravship / TryFindSpotOffGravship` 找格，发 `GotoShip` / `LeaveShip`（Jog）；找不到格子会发负面消息并把 pawn 移出列表。上船前会取消正在进行的仪式 lord。
- `JobGiver_GoToValidSubstructure.cs`：仪式（重力舰发射仪式）参与者经 `CompPilotConsole.engine` 找到 GravEngine，`GravshipUtility.IsOnboardGravship` 判定已在船上则原地等待，否则 `TryFindSpotOnGravship` 上船。
- **海洋类比**：若做"潜水钟/潜艇/月池舱"作为海底与海面之间的载具，这套"引擎建筑持有 pawnsToBoard 列表 + 找船上落点 + GotoShip"的流程是最接近的原版模板。

## 7. 其余文件（PASS 概览）

- **战斗 AI 族**（AIFightEnemy/AIFightEnemies/AIDefend*/AIBreaching/AISapper/AITrash*/Manhunter/Berserk/Shambler*/Revenant*/Nociosphere*/Chimera/Sightstealer/Metalhorror 等约 50 个）：通用索敌/走位/破墙逻辑，与海洋无机制交集。唯一的侧面信息：破墙（Breaching/Sapper/Shambler 的 `PassAllDestroyableThings` 寻路）不关心房间气密性——海底基地被"凿墙灌水"的威胁与原版凿墙完全一致，无需新 AI。
- **需求族**（GetFood/GetRest/GetJoy/GetEnergy/GetHemogen/GetDeathrest/SatisfyChemical*/Meditate/Learn 等）：标准 need 驱动，除 GetFood 的钓鱼分支外无海洋点。
- **仪式/社交/搬运/商队/囚犯族**：均与海洋无关。`JobGiver_PrisonerEscape` 只认 `District.TouchesMapEdge`（海底图边缘是致死海水，囚犯照样会"逃"进海里——未验证这是否是想要的效果，属边缘案例）。
- `JobGiver_Forage.cs`：要求 `GetFertility >= 0.7 && terrain.natural`；海底泥地形 fertility 若为 0 则不触发（未验证海底地形的 fertility 设定）。
