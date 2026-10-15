> 主代理游戏验证：`mod31-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod31-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod31-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod31-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 beta 升级记录

状态：已适配，真实游戏验证未完成。基线 `1.7.4977 rev926`；发布 ID `3226563448`；packageId/程序集/命名空间保留原拼写 Havesting。

新建 1.7/Source 与 AutomaticBioferriteHavesting.dll，源码与 1.6 完全相同。新工程 net10.0、精准 Krafs.Rimworld.Ref 1.7.4977-alpha.1；删除模板中未使用的 Harmony/HugsLib 编译包，原/新完整静态引用证实均无相应 API，无需 Publicizer 或运行时 Harmony。About 添加 1.7，其原 Anomaly 依赖保留。旧 1.5/1.6、Common XML/语言和备份逐字节不变；默认加载规则选择 1.7、Common、根目录，单份同名 DLL。

已完整审查全部自有构造、Props/thing 属性、gizmo iterator 与启用/toggle lambda、CompTick：CompProperties.compClass/ThingComp.props,parent；Building_BioferriteHarvester.containedBioferrite/TakeOutBioferrite；IsHashIntervalTick(250)、Position/Map；GenPlace.TryPlaceThing(Near)；Command_Toggle 的标签/说明/图标/委托/activateSound，Translate、ContentFinder<Texture2D>、SoundDefOf.Tick_Tiny。无 Harmony、反射、DefOf、自有纯计算或序列化入口。

1.7 真实 `Verse/Thing.cs:700` DoTick 对 Normal ticker 仍先调用 Tick，再处理 TickInterval；Building_BioferriteHarvester.cs:124 的 Tick 先 base.Tick；ThingWithComps.cs:322 仍遍历 CompTick，所以旧 override 没有变成不再调用的钩子。原版每 250 hash tick 生成内容并上限 60；自有组件同周期在阈值 60 时调用真实 TakeOutBioferrite（取整、扣除、创建 Bioferrite），随后 Near 放置。该时序/算法与原源意图一致，不能仅因出现新 TickInterval 就机械改旧入口。

Common PatchOperationAdd 在 Anomaly 条件下给官方 BioferriteHarvester.comps 加内部组件属性，阈值 60；实际 1.7 官方 Def 和相关公开字段存在。内部类型名与 ctor、compClass 保留，运行时反射创建待真实游戏验证。唯一英文 Keyed 文件保留；默认说明中的占位符是既有内容，本轮不顺手改文案。没有自有 Scribe：harvestingEnabled 默认 true，原开关并未持久化，本轮未添加新存档字段或改变旧保存行为。

已执行 `scripts/dotnet.sh restore work/31-AutomaticBioferriteHarvesting/1.7/Source/mod.csproj --configfile .nuget/nuget.config` 与 Release build，0 warnings/errors（restore.log/build.log）。原/新成员和类型 metadata 索引在 logs/31-AutomaticBioferriteHarvesting/api-original.json、api-new.json；XML/字节保留/单 DLL 检查通过（static-validation.json），无复制游戏/Unity/Harmony DLL。旧 1.6 构建引用为 1.6.4488-beta；旧反编译的构建出处未核验，未冒充可靠旧语义基线。

无现有单元测试，所有自有路径涉及真实组件/地图/命令和 ticker。Tests/1.7/Mod31Checks.cs 已精准 Ref 单独编译（0 warnings/errors，checks-build.log），但未执行。它在临时采集器中确认唯一组件及阈值，调用真实建筑 DoTick，覆盖低于阈值、非 250 hash tick、到阈值弹出实际 60 个灵铁、gizmo 关闭/再开弹出第二批；finally 恢复 tick、销毁临时建筑/新物品及恢复可能合并的旧物品数量。该断言覆盖生产关键入口而非只调用补丁自身或复刻条件。

待主代理单 MOD+Anomaly 加载和 Mod31Checks 实际断言；还需正常持有实体生产过程、UI 图标/开关操作和一次性存档重载。Headless 的真实 tick/物品断言不能证明可见 GUI。实际 tick 推进/存档与未序列化开关的范围单独记录，不宣称全功能或跨 MOD 验证完成。
