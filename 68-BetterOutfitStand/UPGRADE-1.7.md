> 主代理游戏验证：`mod68-rev1248-i`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod68-rev1248-i`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Better Outfit Stand：1.7 beta 升级

基线：本机 `1.7.4977 rev1248`、真实快照 `RW/17/Assemblies` 与 `RW/17/Source`；原版为仓库 1.6。Workshop ID `3540103312`，packageId `RunningBugs.BetterOutfitStand` 保持不变。仅工作区暂存适配，真实游戏由主代理执行。

## 最小修改与布局

- About 增加 1.7；新增独立 `1.7/Source`、`1.7/Assemblies/BetterOutfitStand.dll`，1.6 源码和产物逐文件比较保持原字节。
- 新项目 `net10.0`，锁定 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Lib.Harmony 3.0.0-rc.2`、`Krafs.Publicizer 2.3.2`。
- 仍仅 publicize `RimWorld.Building_OutfitStand.SetAllowHauling`，运行策略 `IgnoresAccessChecksTo`；不公开其他私有成员，不改变保护级别的渲染 override。
- 新版启动仅一行 `.PatchAll(typeof(BetterOutfitStandHarmony).Assembly)`，规避 Harmony 3 无参栈回溯可能被内联影响的问题。其余产品 C# 方法正文保持原样。
- 既有结构测试增加 `BOS_TEST_VERSION` 参数，默认仍检查 1.6。根目录没有 Assemblies、没有 LoadFolders；默认只选新版 1.7+Common+根。输出只有本 MOD DLL、deps.json、PDB，不附带游戏/Harmony 引用 DLL。

## API 与语义复核

完整静态 MemberRef/TypeRef 清单：工作区 `logs/68-BetterOutfitStand-api-audit.json`。游戏/Unity/Harmony 可识别直接成员 184 项；泛型签名、继承重写、XML、Scribe 与补丁入口另读源码。共享 metadata 比较证明所有 Assembly-CSharp 合同在旧 rev926、新 rev1248、alpha.1 三者相同，不能据此推导任意引擎 API。

- 三个实际 Harmony Postfix：`ThingWithComps.GetGizmos()`、`Building_OutfitStand.Notify_ItemAdded(Thing thing)`、`Notify_ItemRemoved(Thing thing)`。实际 `ThingOwner` 添加/移除仍调用这些通知；原参数名/枚举返回类型匹配，未使用 transpiler。
- 原生 `Building_OutfitStand` 构造、`HeldItems`、`HeldWeapon`、`AddApparel`、`RemoveApparel`、`TryDrop`、`RemoveHeldWeapon`、`TryAddHeldWeapon`、`GetStoreSettings`、`CanEverStoreThing`、`GetFloatMenuOptions` 以及 protected 渲染属性保持签名及容器语义。持有武器缓存由 Notify_ItemAdded/Removed 维护；原生存放限制仍由 fixedStorageSettings 决定，MOD 仅允许可变过滤器。
- 私有 `SetAllowHauling(bool)` 仍只切换 allowRemovingItems 并重新计算 haul-source；原 UI Confirm 的调用作用保持。只有此成员需 publicizer。
- 服装检查仍使用 `PawnCanWear`、`HasPartsToWear`、`CanWearTogether`、CompBiocodable；`Pawn_ApparelTracker.Remove/Wear`、OutfitForcedHandler.SetForced 保持实际转移/强制服装语义。native MakeRoomFor 的 out 参数是已生成在地图上的旧武器，原源码 DeSpawn 后加入架子正确；不能把新版本地图 ParentHolder 非空当作物品仍在库存。
- JobDriver 的 Notify_Starting→SetupToils、Reserve、GotoThing、Delay 与最终 finish action 流程仍存在。Scribe 字段 `duration`、三套转移集合及设置键 `BOS.SelectAllByDefault` 保持原标签；未变更存档数据格式。
- XML 确认 Odyssey 实际 `Building_OutfitStand`、`Building_KidOutfitStand` 及本 MOD `UseOutfitStand_Better` 目标存在；儿童架需要 Biotech。未为缺少 Odyssey 的组合增加新依赖/行为。
- `Rect/Vector2`、Verse Widgets、Window/Listing、GetSettings、Command_Target、TargetingParameters、原生 PutApparelOnOutfitStand/HaulToContainer、予約/年龄/工作标签检查均可编译匹配。本 MOD 不调用此次 rev1248 新 IME/引擎显存 API。

## 验证与可重复命令

从工作区运行：

```bash
scripts/dotnet.sh restore work/68-BetterOutfitStand/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/68-BetterOutfitStand/1.7/Source/mod.csproj --no-restore -c Release
scripts/dotnet.sh run --project work/68-BetterOutfitStand/Tests/1.7/RectChecks/RectChecks.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh build work/68-BetterOutfitStand/Tests/1.7/Mod68Checks.csproj -c Release --configfile .nuget/nuget.config
```

- 产品 Release：0 warnings、0 errors，日志 `logs/68-BetterOutfitStand-build.log`；DLL SHA256 `e870b578ae2776e9d76c87da9eca80fbfacdb3bae8a5e65fca631347da9a5e82`。
- 真正纯逻辑测试直接编译产品 RectUtils 并加载本机真实 UnityStubs，验证两方向、非零原点、零/内部/全长切分的覆盖、边界连续及无重叠不变量，通过。`logs/68-BetterOutfitStand-rect-tests.log`。没有用伪 Rect 或复刻实现。
- 既有结构测试通过 1.6/1.7 各 8 项，以 `.venv/bin/python` importlib 加载函数运行；`logs/68-BetterOutfitStand-structural-tests.log`。这些字符串检查仅证明结构，不代表运行功能。
- `Tests/1.7/Mod68Checks.cs` 仅引用游戏/Harmony，实际 MOD 类型通过反射读取，独立编译 0/0。交给主代理在完整 Playing 主线程运行：实际补丁注册、成人/儿童 XML 类、真实原生服装架/人类 Pawn、原生通知重开过滤、地上武器 gizmo/目标、真正 JobDriver startup 和其注册的最终转移委托、双向衣物/强制 outfit、真实装备交换、保持暂停的自动禁用过滤器、右键选项。fixtures 使用 real ThingOwner/Pawn 及 native API；finally 清理测试物品/Pawn/Job。跳过寻路行走和装备等待的 tick，不冒称端到端完成 Job。

## 尚未验证与已有风险

真实地图断言尚待主代理执行。窗口绘制、两列 checkbox/滚动/Confirm 私有访问、选中殖民者的实际 command action/移动搬运、完整 Job travel/duration、儿童穿戴、biocode/身体缺失/锁定服装、保存重载仍未测。Headless 不能证明图形交互兼容。

审查发现原有 PawnDropMode 对锁定服装仅 continue，但不会从转移集合移除；DoTransfer 后续仍遍历该集合。此为原有逻辑风险，尚未通过真实输入验证，不作为 1.7 API 差异顺手修复；原旧版保持。自动过滤器禁用明确保持 false，未改变用户当前暂停行为。
