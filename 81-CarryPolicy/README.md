# CarryPolicy（携带方案）

RimWorld 1.6 / 1.7 beta MOD：模仿原版成瘾品方案体系的**通用携带方案**。配置命名方案（带哪些物品、各带多少），
在原版**分配（Assign）页签**的「携带方案」列指派给小人，小人自动从地图补货并保证不被丢弃/搬走。

## 用法

1. 分配页签 → 每个小人都有「携带方案」下拉框；表头按钮「管理携带方案」打开管理弹窗。
2. 管理弹窗（仿原版方案弹窗）：
   - 左侧方案列表：新建 / 删除 / 复制 / 重命名 / 设为默认；
   - 内容区双栏：**左=携带清单**（图标+名称+数量+≈件数+删除），**右=可选项**
     （类别下拉 + 搜索跨类目 + 多选 + 「添加所选」批量加入）；
   - 默认方案为空（什么都不带），对原版行为零影响。
3. 可选范围 = 一切能进背包的物品（Item 且可搬运；尸体除外）。

## 「份」的定义（重要）

食物条目的数量单位是**份**，不是"个"：

- 1 份 = 普通成年人一顿饭的量 = 0.9 营养 ≈ 可维持 13.5 小时
  （普通成人饥饿消耗 1.6 营养/天，由 1.6 源码 `Need_Food.BaseHungerRate` 核实）；
- 运行时按每个小人自身的饥饿消耗折算营养量，再除以单件营养得到目标件数：
  牛奶(0.05/个) 1 份 ≈ 18 个；简单食物 1 份 = 1 个；
  大食量种族（mod 种族/基因）1 份对应的营养更多，自动多带；
- 非食物条目的数量就是件数（如草药 5 = 带 5 个）。

UI 行内的 `≈N件` 为按普通小人的估算值；列头悬停有完整说明。

## 营养膏

营养膏不会预先做好放在地图上，小人无法用普通方式捡取。若方案含营养膏（或 mod 中
`Building_NutrientPasteDispenser` 子类的产物），小人会**优先捡起地图上现成的**（含库存里
已储存的；被其他小人预留搬运的物品会因 `CanReserve` 校验而自动跳过，不会抢夺），
找不到才走到可用的营养膏机现场取一份放进背包（一次一份，逐步补满，不会一次清空料斗）。

## 行为实现

- `JobGiver_CarryPolicy` XML插入Humanlike，优先取现成库存，缺少才找真实膏机。显式携带清单不受能否/愿否食用门控；使用重量/可达/预留检查。无缺口300~600 ticks短间隔，有缺口但找不到2500~5000长退避；节流表每局新建。
- `Pawn_InventoryTracker.FirstUnloadableThing` getter Prefix：完整扫描，合并原药物/库存/食物保留量与携带配额，取最大值，不跳过后续真正需卸载货物。
- `JobGiver_DropUnusedInventory.Drop` Prefix：不足或等于配额不丢，超额仅丢超出部分；没有第三个ShouldKeepDrug补丁。

## 构建

```
cd 1.6/Source && dotnet build CarryPolicy.csproj -c Release
```

输出到 `1.6/Assemblies/CarryPolicy.dll`。依赖 NuGet：`Krafs.Rimworld.Ref 1.6.*`、`Lib.Harmony 2.3.1.1`。

## UI 原型

`sim/carry_policy_dialog_sim.py`（[rimworld-imgui-sim](../../rimworld-imgui-sim)）渲染的弹窗原型图
在 `sim/out/`，布局常量与 C# 实现对应；`vision_review.py` 用 OpenRouter 视觉模型做 UI 审查。

## 已知边界 / 后续计划

- 支持独立1.6与1.7 beta；
- 商队中的自动补货未实现（后续可仿 `CaravanDrugPolicyUtility`）；
- 取膏一次一份；
- 未特别处理 mutant `disablePolicies`（食尸鬼等仍可指派方案）。

## 1.7 beta 构建与验证

`1.7/Source/CarryPolicy.csproj`使用net10.0、固定Krafs.Rimworld.Ref 1.7.4977-alpha.1与Lib.Harmony 3.0.0-rc.2，产物 `1.7/Assemblies/CarryPolicy.dll`。旧1.6保持。实际验证范围/日志/API/保存重载限制见 `UPGRADE-1.7.md`。sim只为UI原型，不证明真实游戏交互；本轮不调用外部视觉服务。
