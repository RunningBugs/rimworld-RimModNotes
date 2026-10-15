> 主代理游戏验证：`mod11-rev1248-g`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod11-rev1248-g`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录：已适配，实际游戏验证待执行

基线 `1.7.4977 rev1248`。上游本机 Roads of the Rim (Continued) 1.6.2 / `mlie.roadsoftherim` 只声明到 1.6；LoadFolders 无 v1.7，1.7 将回退 1.6 + Assets。上游旧标签本身不能证明不兼容：已核对实际安装接口与消费语义，新增本补丁独立 1.7 工程和支持声明，由主代理先验证上游基线。不得把编译或标签当成实际功能通过。

## 实证审查

本补丁实际没有 Harmony 补丁：`RoadOnIce.MyMapComponent.FinalizeInit` 遍历 **所有** BiomeDef，将 allowRoads=true（范围大于说明中的 IceSheet/SeaIce，保持旧行为）。独立静态启动只记录日志；IsModActive 方法未被使用。1.7 `Source/Verse/MapComponent.cs:32` 虚 FinalizeInit、`Source/RimWorld/BiomeDef.cs:21` 公有 allowRoads 字段、DefDatabase<BiomeDef>.AllDefs、ModLister.GetActiveModWithIdentifier 和 Verse.Log 都仍存在。无存档字段、有效 XML Patch 或真实 HugsLib 使用；模板引用不能当作依赖证据。

实际安装 DLL 已反编译到 `logs/11-RoadOnIcePatch/upstream-1.6`：`DefModExtension_RotR_RoadDef.BiomeAllowed(int,RoadDef,out BiomeDef)` 第 74–101 行确实读取对应 Surface.Tiles 的 PrimaryBiome.allowRoads，因此旧补丁不是死路径。上游另有 RoadConstructionSite、世界路径/道路 Harmony 补丁及资源渲染，这些整 MOD 的 1.7 初始化/调用链兼容尚未验证。仅有字段存在不足以确认上游功能可用。

旧本补丁 DLL 的完整静态引用已留在 `api-references-baseline.json`，它是 1.4 旧产物审查，**不是 1.7 构建证据**。上游来源/哈希在 dependencies.json；未修改 Workshop、源仓库、上游版本声明。

## 1.7 产物与待测项

独立 `1.7/Source` 逐字复用原生产 C#，net10.0 + 精准 Krafs.Rimworld.Ref 1.7.4977-alpha.1，移除未使用的 Harmony/HugsLib 编译引用，不需要 Publicizer；原 Source/1.4 文件及 DLL 保留。About 新增 1.7，默认只选新版本 DLL，空模板 XML 无需复制。生产与 `Tests/1.7/Mod11Checks.cs` 均编译 0 warning / 0 error，分别见 build.log、checks-build.log。实际静态引用留在 api-references.json。

Map 场景不是只复刻字段赋值：反射**实际安装上游** BiomeAllowed，使用真实世界 land tile 和已加载带上游 extension 的 RoadDef，先让此真实 biome.allowRoads=false 并要求上游拒绝且返回对应 biome；执行已加入地图的本 MOD MapComponent.FinalizeInit 后要求同一上游入口接受并核查两类冰 biome。finally 恢复所有全局 biome 标志。完整道路放置/施工 UI 和跨存档全局标志仍待验证。没有可脱离 DefDatabase/地图生命周期的独立自有逻辑，因此单元测试不制造桩；真实 consumer 对照由 root 串行实跑。

命令 `bash scripts/dotnet.sh build work/11-RoadOnIcePatch/1.7/Source/mod.csproj -c Release --no-restore`。递归运行依赖 `brrainz.harmony`、`mlie.roadsoftherim`；上游路径 `/Data/SteamLibrary/steamapps/workshop/content/294100/2280318231`，未修改 Workshop，也未启动游戏。
