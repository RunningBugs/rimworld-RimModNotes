# AAT 高性能架构草案

## 结论

RimWorld/Unity 的 `Thing`、`Map`、`DesignationManager`、`ListerThings`、`ListerHaulables`、`PathFinder`、`StoreUtility`、`ReservationManager` 等对象都应视为主线程对象。AAT 可以使用后台线程，但后台线程只能处理主线程采集出来的不可变快照，例如 `thingIDNumber`、`defName`、`Position`、简单 bool 标记、候选 ID 集合排序/分桶；不能在后台线程直接读写 RimWorld 对象。

## 主线程必须执行

- 读取或修改 `Thing` / `Map` / `Pawn` / `Designation` / `Area` / `Selector`。
- 调用 `StoreUtility.TryFindBestBetterStorageFor`、`HaulAIUtility.PawnCanAutomaticallyHaulFast`、`CanReach`、`CanReserve`。
- 调用 `SetForbidden`、`AddDesignation`、`RemoveDesignation`、`Messages.Message`、`SelectionDrawer.Notify_Selected`。
- 访问 Unity 输入/GUI：`Event.current`、`Input.GetKey`、`ContentFinder<Texture2D>`、`FloatMenu`。

## 可后台线程处理

后台线程只能处理主线程快照，例如：

```csharp
public readonly struct AATThingSnapshot
{
    public readonly int ThingId;
    public readonly string DefName;
    public readonly string StuffDefName;
    public readonly IntVec3 Position;
    public readonly bool InHomeArea;
    public readonly bool IsForbidden;
    public readonly bool IsUrgentDesignated;
}
```

可后台做：
- 大列表按 `defName/stuff` 分组。
- 右键菜单候选项文本预计算。
- 大规模 select-similar/allow-all 的候选 ID 排序和去重。
- 不依赖 path/storage/reservation 的纯过滤。

后台线程完成后必须把结果投递回主线程，并在主线程用 `thingIDNumber -> Thing` 重新解析且重新验证 live/map/fog 状态。

## 推荐架构

### 1. 每 Map 一个 cache component

- `HaulUrgentlyCache : MapComponent`
- dirty + 低频过期重建。
- 候选集为：`urgent designation ∩ map.listerHaulables.ThingsPotentiallyNeedingHauling()`。
- `PotentialWorkThingsGlobal` 只返回缓存候选；pawn 级检查在 `JobOnThing`。

### 2. 大规模工具使用主线程分片

Allow all、forbid area、harvest all、select similar 全图版本不要一次做完全部昂贵检查；优先：

- 对几十/几百对象：单帧直接执行。
- 对几千对象：主线程 `MapComponentTick` 分片，每 tick 处理 N 个。
- 每个 tick 处理 budget，例如 250 个 Thing 或 1-2ms。

这比后台线程更安全，因为这些操作最终都要访问 RimWorld 对象。

### 3. 后台线程只做快照纯计算

可新增：

```csharp
public sealed class AATBackgroundPlanner
{
    public Task<IReadOnlyList<int>> PlanAsync(IReadOnlyList<AATThingSnapshot> snapshot, CancellationToken token);
}
```

但当前 AAT 的已实现功能主要瓶颈是 storage/path/reservation，这些不能后台调用。因此第一阶段优先采用主线程缓存和分片，不急于引入线程复杂度。

## 当前实现采用的策略

- Haul Urgently：主线程缓存 + vanilla lister 交集 + no-storage 60 tick 缓存；urgent designation 查询直接使用 `designationManager.designationsByDef[HaulUrgentlyDesignation]`，避免 `AllDesignations` 的全量合并和 static tmp list。
- Allow/Forbid/AllowAll：主线程直接操作，过滤只做 forbiddable/live checks；AllowAll 是 O(map.listerThings.AllThings)，通常低频手动触发。
- Select Similar：主线程 UI/selector 操作；gizmo 判断使用 for-loop 避免 LINQ GC；逐格 designator 操作先复制 `ThingsListAtFast` 结果到临时列表，避免遍历内部可变列表时修改选择/状态。
- Harvest Fully Grown：主线程 plant lister 过滤；逐格 designation 先复制临时列表；后续可分片处理全图 harvest all。

## 白盒测试策略

由于 RimWorld runtime 对象不可安全在外部 runner 构造，当前白盒测试采用源码/XML 结构断言，覆盖：

- XML 可解析，设计器注册完整。
- `PotentialWorkThingsGlobal` 不调用 pawn 级昂贵搬运检查。
- `JobOnThing` 保留 pawn 级检查。
- Haul cache 与 `listerHaulables` 求交集。
- add/remove designation dirty cache patch 存在。
- cell/container deposit 后清理 urgent designation。
- 图标引用均存在。

运行：

```bash
Tests/run_whitebox.sh
```
