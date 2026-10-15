# 1.7 验证场景

静态检查已执行：工作区 `.venv/bin/python work/66-CaravanShuttleMassInfoInspectString/Tests/1.7/validate_layout.py`。
它验证 XML、身份、旧文件、版本加载目录和输出，不能证明游戏方法执行成功。原项目没有单元测试；自有函数仅组件属性构造函数和依赖游戏对象的 `CompInspectStringExtra`，没有适合脱离游戏单独执行的纯计算逻辑。

以下功能场景尚待主代理在隔离游戏内执行；测试代码不进入发布 DLL。

可直接合并进共享测试 MOD 的 `Mod66Checks.cs` 使用约定 `RW17.Validation.Mod66Checks.Run(Verse.Map)`，无需引用本 MOD DLL，只通过组件类型名查找已加载对象。它已独立编译通过（0 warnings/errors），但尚未在游戏内执行。自动断言覆盖组件唯一挂载、空/null/无穿梭机、实际私有重量调用、物资变化、完整 GetInspectString 链和移除穿梭机；生成对象用 finally 清理。可视、超重及保存重载仍属待测。

1. 加载 MOD 后，断言 `WorldObjectDefOf.Caravan.comps` 恰好含一个 `ShuttleMassInfo.WorldObjectCompProperties_CaravanShuttleMassInfo`，其 `compClass` 为 `ShuttleMassInfo.CaravanShuttleMassInfoComp`。新建远行队和存档重载的远行队均应有对应组件。
2. 组件的 `parent=null` 或空的 `new Caravan()` 时，调用 `CompInspectStringExtra()` 应返回 null；无穿梭机的真实远行队亦然。注意空 Caravan 仅用于这个方法，不能调用未初始化的世界/面板方法。
3. 在游戏中建立携带真实穿梭机和物资的远行队。反射取得 `CaravanShuttleUtility.GetCaravanShuttleMass`（`BindingFlags.Static | BindingFlags.NonPublic`），按 `(float)method.Invoke(null, new object[]{caravan})` 取得游戏重量计算结果。组件字符串必须包含该结果的一位小数、`shuttle.TransporterComp.MassCapacity` 的一位小数及 `Mass` 当前语言翻译；`caravan.GetInspectString()` 应包含组件的整行字符串。
4. 改变物资/成员后刷新物品缓存，再次检查组件与游戏原生计算一致；构造超重情形，和 `CanLaunchCaravanShuttle` 报告中的同一计算值对照。不要用 `caravan.MassUsage` 替代穿梭机计算，后者包括小人质量并扣除穿梭机自身重量。
5. 可视运行中选中世界地图远行队，确认实际面板显示、不重复、不含尾部空白，中文/英文均可读。Headless 可以验证字符串与组件挂载，不能验证面板绘制。
6. 对一次性夹具保存、重载，确认组件仍可通过 Caravan Def 重建且字符串一致；本 MOD 无额外存档字段。

真实 DLL 的私有访问依赖 Publicizer 生成的 IgnoresAccessChecksTo 和 Waylith 访问兼容机制，必须用场景 3 执行确认，不能仅凭编译通过。
