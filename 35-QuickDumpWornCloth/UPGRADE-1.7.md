> 主代理游戏验证：`mod35-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod35-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod35-wave2`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod35-wave2`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，游戏验证未完成。基线 `1.7.4977 rev926`，发布 ID `3440271594`，packageId `RunningBugs.QuickDumpWornCloth`。

新增独立 1.7/Source 与 QuickDumpWornCloth.dll，net10.0、精准 Krafs.Rimworld.Ref 1.7.4977-alpha.1；原/新 metadata 没有 Harmony/HugsLib API，移除新工程两个模板包，无需 Publicizer。保留旧 1.5/1.6/Common/备份，About 加 1.7；默认加载仅选择新同名 DLL。生产改动只额外加 System.Linq using，并在 action 的 foreach 使用 thingsToDump.Distinct()。

去重修复针对静态可证实的既有缺陷：同一 Apparel 同时死人衣物和敌人/无主生物编码时，两条独立条件会把同一对象加入两次；第一次已通知抛弃、移除所有权并 Destroy，第二次找不到 owner 而 Log.Error。仅在新 1.7 action 去重，原筛选规则/销毁功能不变，旧版源未修改。主代理已认可此最小修正。

完整 API/语义审查覆盖启动 Log、WorldObjectCompProperties.compClass 构造、WorldObjectComp.GetGizmos override、parent/Caravan；AllInventoryItems/GetOwnerOf；Apparel.WornByCorpse；CompBiocodable.IsBiocoded/CodedPawn/Biocoded，Pawn.Faction/Faction.OfPlayer；TryGetComp；Command_Action 属性/action；Notify_AbandonedAtTile、GetTileCurrentlyOver、Pawn.inventory.innerContainer.Remove、Thing.Destroy、Caravan.RecacheInventory。没有生产 Harmony、反射、transpiler 或自有存档字段。

真实 1.7 WorldObject.GetGizmos:620 仍遍历 comps[i].GetGizmos，Caravan.GetGizmos:719 仍枚举 base，旧 override 是有效入口。库存仍在 Pawn_InventoryTracker 的 ThingOwner 中；GetOwnerOf 检查该实际 ParentHolder，非地面 null 假设，符合共享的 1.7 所有权变化。抛弃通知现在按 PlanetTile 编译绑定，真实 Thing.Notify_AbandonedAtTile(PlanetTile) 保留。Biocodable 公共属性和实际编码语义不变。Common Patch 仍给官方 Caravan WorldObjectDef 加属性组件，静态 target 存在，实际挂载待游戏验证。

已执行 restore/Release build：`scripts/dotnet.sh restore work/35-QuickDumpWornCloth/1.7/Source/mod.csproj --configfile .nuget/nuget.config` 与 build --no-restore --configuration Release，0 warnings/errors。原/新完整静态引用 logs/35-QuickDumpWornCloth/api-original.json、api-new.json；static-validation.json 记录 XML/旧文件逐字节保留/单 DLL 检查，未复制游戏/Unity/Harmony DLL。旧原工程使用 Ref 1.6.4488-beta；RW/16 实现具体构建出处未核验，不声称完整旧语义基线可靠。

所有自有行为函数依赖真实组件/库存/地图格和命令；无已有独立测试，未复刻筛选实现做假单测。Tests/1.7/Mod35Checks.cs 编译 0 warnings/errors（checks-build.log），尚未执行：通过真实 caravan.GetGizmos 枚举和 action 删除死人衣物、敌人生物编码、编码人缺失及双条件重叠；保留干净衣物和玩家编码物品；删除后按钮消失，并覆盖空远行队/null parent。测试专用临时 Harmony spy 验证重叠 Apparel 实际 Notify_AbandonedAtTile 恰好一次，最终 unpatch/清理所有生成对象。Harmony 是测试驱动依赖，不是生产 MOD 依赖。

待主代理真实功能断言与日志比较，尤其重叠输入无新增 Log.Error；另需可视按钮交互、保存重载和其他库存变动时机。原按钮标签/说明仍是原英文常量，本轮不改文案。测试不使用真实用户存档；不存在未验证第三方 DLL 引用。
