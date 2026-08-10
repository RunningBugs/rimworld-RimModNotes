# 06 - InventoryTab 列表闪烁原因分析

> MOD：InventoryTab（SirKota，Workshop ID 1509278793，packageId `Kota.InventoryTab`）
> 依据：MOD 自带源码 `Source/InventoryTab/InventoryTab/`，并用 ilspycmd 反编译实际加载的 `1.6/Assemblies/InventoryTab.dll` 验证一致。
> 现象：打开库存标签页后，列表中的物品行每隔几秒就上下换位（观感为"闪烁"）。

## 结论：不稳定排序 + 缺失平局决胜规则 + 不稳定的输入顺序

闪烁由三个因素叠加产生：

### 1. 比较器对平局返回 0（`Slot.cs:36-64`）

`Slot.CompareTo` 只比较 `MarketValue`（单价），相等时直接 `return 0`——作者原本写了"同市值按名称排序"的平局决胜代码，但**被整段注释掉了**。实际加载的 DLL 同样没有决胜规则（已反编译验证）。

### 2. `List<T>.Sort()` 是不稳定排序（`MainTabWindow_Inventory.cs:201`）

`DrawMainRect` 每帧调用 `categorizedSlots.Sort()`。.NET 的 `List.Sort` 是 introsort，**不保证相等元素的相对顺序**——相等元素的最终位置取决于它们在**输入列表里的顺序**。

### 3. 输入列表的顺序每次重建都在变（`MainTabWindow_Inventory.cs:118-123` + `ItemFinderHelper.cs:14-39`）

- 自动更新默认开启、默认间隔 **5 秒**（`OptionsHelper.cs:27`），每次触发 `UpdateThings()` 全量重建列表；
- `GetAllMapItems` 按 `map.listerThings.AllThings` 的内部顺序遍历——殖民地搬运工不断捡起/放下/合并堆叠，这个顺序**一直在变**；
- `CombineThings` 用 `Dictionary<string, Slot>` 按名称分组后按 `Values` 顺序倒出（`MainTabWindow_Inventory.cs:322-367`），其枚举顺序又跟随插入顺序，即跟随上面那个不稳定的顺序。

于是每过一次自动更新：输入顺序变化 → 不稳定排序对"市值相同"的物品给出不同的排列 → 列表里同价位的物品行互换位置 → 视觉上就是列表在"闪烁"。录像里 `高级零部件(x38)` 与 `次生核心(基础)(x1)` 反复互换就是这种情况。叠加堆叠数量变化（x38→x37），观感更明显。

## 修复建议

恢复并补完平局决胜（让排序结果与输入顺序无关，彻底确定性）：

```csharp
// Slot.cs
public int CompareTo(Slot other) {
    int cmp = other.thingInSlot.MarketValue.CompareTo(thingInSlot.MarketValue);
    if (cmp != 0) return cmp;
    cmp = string.Compare(thingInSlot.LabelNoCount, other.thingInSlot.LabelNoCount, StringComparison.CurrentCulture);
    if (cmp != 0) return cmp;
    return string.Compare(thingId, other.thingId, StringComparison.Ordinal);  // 分组键唯一，保证完全确定
}
```

## 临时规避（不改代码）

在 InventoryTab 设置（右上角齿轮）里**关闭 AutoUpdate**，列表只在打开/搜索/切页时重建，闪烁消失；代价是数量不会自动刷新，需要手动点搜索按钮刷新。
