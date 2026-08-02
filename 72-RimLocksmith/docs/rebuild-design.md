# RimLocksmith 重做设计(融合 Locks 逻辑)

日期:2026-08-02
前置文档:`vanilla-behavior-audit.md`(原版行为对照与过度修改审查)
参考:工坊 Mod Locks(id 1157085076,1.6 版 Locks.dll 反编译)、原版 1.6 反编译

## 0. 本次要修的 bug(根因已坐实)

狂暴动物呆住:`JobGiver_Manhunter` 在目标不可及时,用 `PassDoors` 模式
(`canBashDoors=false`)探测通向目标的路径,走到"阻挡门之前最后一格"并在
附近游荡(= 原版"包围房子不破门"行为)。原版 PassDoors 对开不了的门计
代价 150,路径总能找到;当前 Mod 的 `GetDoorCost` prefix 对 PassDoors 也套
LockPolicy,`canBashDoors=false` → 返回 `ushort.MaxValue` → 路径找不到 →
`TryGiveJob` 返回 null → 狂暴动物无任何 job,呆立原地。

**Locks 完全没有 patch 寻路**,这也是它没有此类 bug 的原因。本方案删除
`GetDoorCost` patch。

## 1. Locks 的逻辑与分类(反编译摘要)

`LockUtility.PawnCanOpen`(transpiler 整体替换原版方法)判定顺序:

1. **特殊放行最优先**:`lord.LordJob.CanOpenAnyDoor(p)`(越狱/叛乱/商队/
   远行队)或 `guest.Released` → 直接 true;
2. 异象变异体 `!canOpenDoors` → false;
3. 无派系门 → `canOpenFactionlessDoors`;
4. `GetRespectedState`:囚犯/敌对/精神崩溃中用 CurrentState,正常住民用
   WantedState(Locks 有 flick 工作延迟,我们即时生效,无需此机制);
5. 按**生物类型**分派:Humanlike / Animal / Mechanoid,各处理器**第一条
   都是"敌对派系 → false",不可配置**;
6. 动物处理器:无派系动物(野生动物)→ false,不可配置;有
   仅宠物(体型≤0.86 同派系)/畜栏门(PensDoor + FenceBlocked +
   TradeWithColony 放行)等子选项;
7. 配置面:LockMode(Allies/Colony)、殖民者名单、奴隶名单、儿童锁、
   动物三态、机械体(任意/仅机械师/名单)。

Locks 另有 `LordJob_PrisonBreak/SlaveRebellion.CanOpenAnyDoor` 的**可选**
关闭 patch(默认让原版越狱/叛乱生效)——我们不需要,直接跟随原版。

## 2. 新架构:postfix 只收窄 + Locks 式分类

**核心不变量:Mod 只可能在原版结果上把 true 改成 false,永远不把 false
改成 true。敌人、野生动物、原版特殊路径,结构性不受 Mod 影响。**

`Patch_BuildingDoor_PawnCanOpen` 由 prefix 改为 **postfix**:

```
if (!__result) return;                    // 原版已拒绝(敌对/无派系动物/围栏阻挡等),不动
if (门不是殖民地门 || 无 comp) return;      // 非殖民地门完全旁路
if (pawn.CanOpenAnyDoor) return;          // 原版特殊放行:越狱/叛乱/商队/远行队/野人/变异kind
category = Classify(pawn);                // 见第 3 节
if (category 不可配置) return;             // Hostile/WildAnimal/Other/Prisoner:跟随原版
__result = config.Allows(category);
```

- 原版 `PawnCanOpen` 的全部判定(CanOpenDoors、doorsAlwaysOpen、
  CanOpenAnyDoor、FenceBlocked、guest.Released、MachinesLike)自动保留,
  审查文档 A1–A8 全部消除,且不怕未来版本判定顺序变化;
- 手工复制的 `IsFenceBlockedRoamer` 整个删除;
- `Patch_PathUtility_GetDoorCost` **整个删除**(原版寻路回调的
  `door.PawnCanOpen` 已是 postfix 收窄后的结果,ByPawn/PassDoors/
  PassAllDestroyableThings 全部恢复原版代价,狂暴动物包围行为回归);
- DoorsExpanded 兼容 patch 同样改为 postfix。

## 3. 分类方式(Locks 式:先生物类型,再身份)

```
Classify(pawn):
  敌对派系(Faction.HostileTo(OfPlayer)) → Hostile          [不可配置]
  无派系                                 → WildAnimal/Other [不可配置]
  pawn.Faction == OfPlayer:
    RaceProps.Animal                     → ColonyAnimal     [可配置]
    IsColonyMech(Biotech)                → ColonyMechanoid  [可配置]
    IsSlaveOfColony                      → Slave            [可配置]
    其余(含变异体/食尸鬼)               → Colonist         [可配置]
  非玩家派系(不敌对):
    RaceProps.Animal                     → VisitorAnimal    [跟随 Guest/Trader 所属派系开关,见下]
    TraderKind != null                   → Trader           [可配置]
    其余                                 → Guest            [可配置]
  IsPrisonerOfColony                     → Prisoner         [不可配置,跟随原版]
```

与原分类的差异(修审查文档 A6):**动物先按派系归属分流**,商队/访客动物
不再误归 WildAnimal;不再设 Ally(好感 75)档——Locks 也没有,Guest 一档
覆盖全部非敌对访客,简化配置面。

可配置开关:Colonists / Slaves / Guests / Traders 四个布尔,加
AnimalAccess(全部/仅宠物≤0.86/禁止)与 MechAccess(全部/仅受控/禁止)
两个三态子选项(用户已确认此范围,不加逐 pawn 名单与儿童锁);
默认预设与多门批量编辑沿用现有。
不可配置(跟随原版):Hostile / Prisoner / WildAnimal / Other。

## 4. 数据与兼容

- `LockConfigData`:保留全部现有字段与 scribe key(旧存档不炸);
  `AllowHostiles/AllowPrisoners/AllowWildAnimals/AllowOthers` 停止读取,
  UI 移除;`AllowAllies` 并入 `AllowGuests`(读取时 `AllowGuests = AllowGuests && AllowAllies` 迁移一次,或简单忽略)。
- comp 注入、默认预设、多选批量编辑、设置页,机制沿用,仅 UI 重绘。

## 5. UI 重绘(imgui-sim 迭代)

- 参考 Locks:`ITab` 为只读摘要(当前门规则一目了然)+ "编辑/复制/粘贴"
  按钮;编辑动作弹独立 Window;
- 我们的 ITab 重绘目标:摘要区(7 个可配置类别的开/关状态,紧凑两列)+
  操作区(应用到选中门/复制/粘贴/重置默认);
- 用 `~/mine/workspace/rimworld/rimworld-imgui-sim` 离线渲染 PNG 迭代布局,
  满意后回填 C#(绘制调用与 `Widgets` 一一对应)。

## 6. 实施步骤

1. 架构改造:postfix 化 + 删 GetDoorCost patch + 新分类 + 配置裁剪;
2. 白盒测试重写(Core 纯逻辑:Classify + Allows);
3. 编译进游戏验证审查文档第 6 节清单(重点:狂暴动物包围、越狱、释放囚犯);
4. imgui-sim 迭代 ITab 布局 → 回填 C# → 编译验证。
