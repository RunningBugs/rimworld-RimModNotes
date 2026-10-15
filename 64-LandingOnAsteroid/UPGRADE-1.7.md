> 主代理游戏验证：`mod64-rev1248-aa`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod64-rev1248-aa`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod64-rev1248-x`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod64-rev1248-x`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod64-rev1248-v`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod64-rev1248-v`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod64-rev1248-q`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod64-rev1248-q`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod64-rev1248-o`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod64-rev1248-o`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Landing On Asteroid：1.7 beta 升级

游戏 `1.7.4977 rev1248`，真实 `RW/17/Assemblies` 与 Source。packageId `RunningBugs.LandingOnAsteroid`、Workshop `3532991747` 保持。新增独立 1.7 net10.0 / Ref `1.7.4977-alpha.1` / Harmony `3.0.0-rc.2`，只修改新启动 `harmony.PatchAll(typeof(ApplyPatches).Assembly)`，初始迁移其余产品正文保持；实际生成后新增下述严格限定起始点修正。About增1.7，README补现代构建。原1.6非缓存源码/产物逐字节相同，不使用 LoadFolders.xml.bak，默认1.7+Common+根，输出DLL/deps.json/PDB且不复制游戏/Harmony。

## API与实际功能复核

清单 `logs/64-LandingOnAsteroid-api-audit.json`（93可识别直接外部成员）；完整产品 Main、静态反射型patch目标、override、XML/Scribe/加载另审查。

- Page_SelectStartingSite.ExtraOnGUI/DoWindowContents(Postfix)、protected DoNext(Prefix)、TileFinder.IsValidTileForNewSettlement(PlanetTile,StringBuilder reason,bool forGravship=false)(Postfix)、private GenStep_Asteroid.SpawnOres(Map,GenStepParams)(Finalizer)均是当前真实有效入口。参数名/返回类型匹配，未transpile。nativeDoWindowContents仍设置GameInitData.startingTile；CanDoNext仍走nativeTileFinder；DoNext仍确认邻近定居点再推进页面。
- 当前 SpawnOres **仍硬 cast `(SpaceMapParent)map.ParentHolder`**，起始殖民地仍使用 Settlement，故finalizer恢复并非可删除冗余。实际mineableCounts/countRange/numChunks、GenStep_ScatterLumpsMineable.count/forcedDefToScatter/forcedLumpSize/Generate保持签名与实体生成语义；当前原生额外图形变化不涉及这些游戏API。
- SpaceMapParent.preciousResource、BasicAsteroidMapParent继承、PlanetLayerOrbit/Surface/FirstLayer/Selected、WorldObjectDef.worldObjectClass、Map.ParentHolder与WorldObjects.MapParentAt/Remove、GameInitData.mapGeneratorDef签名匹配。此处Map.ParentHolder是地图父MapParent，不是散放Thing的ParentHolder判断；共享Thing变化不应机械改动该cast helper。
- MapGeneratorDef `AsteroidBasic`、`Base_Player` 与GenStepDef `AsteroidBasic`真存在；后者实际GenStep_BasicAsteroid继承GenStep_Asteroid，矿物表/块数正值。其选择器过滤IsSubclassOf是现有范围（不包括恰好SpaceMapParent本类），未扩大。
- GameComponent.FinalizeInit在真实新游戏/重载全局初始化仍调用，删除同tile无map旧parent的意义保留。储存矿物是static临时状态，不增加Scribe字段；setting键 `LandingOnAsteroid.defaultMapGenerator`保留并只认可AsteroidBasic/Base_Player。
- Command_Action/LayerGUI/Mouse/Tooltip/Sound/Rect/Color等compile匹配，无私有编译访问，不需要Publicizer。屏幕轨道按钮的真实点击与视觉仍待测。

## 构建与真正生成场景

```bash
scripts/dotnet.sh build work/64-LandingOnAsteroid/1.7/Source/mod.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh build work/64-LandingOnAsteroid/Tests/1.7/Mod64Checks.csproj -c Release --configfile .nuget/nuget.config
```

产品/检查0warnings、0errors，`logs/64-LandingOnAsteroid-build.log`、`...-native-check-build.log`。初始DLL SHA256 `9655524de2156c9c8a553b95005d9428098c852440381536e5a5b306462ae82e`（v2新哈希见results）。全部XML解析通过，旧版保持，没有既有测试或与游戏无关纯逻辑；未写stub生成器。

`Tests/1.7/Mod64Checks.cs`已独立编译，实际执行由主代理：

- 检查初始5个+新增起始点实际patch；当前生成世界真实BasicAsteroid/Orbit与矿物。仅临时移除MOD具体TileFinder Postfix，取得同一真实tile的原版拒绝对照，finally按owner/priority/before/after恢复。实际MOD放行+reason清空，普通已有殖民地不放行。
- 临时真实GameInitData、actual Page.DoWindowContents检查Base_Player新数据保留原fallback及AsteroidBasic真实generator/tile/矿物；调用实际DoNext准备prefix，但不推进真正UI流程。
- 创建一次性player Settlement在真实asteroid tile，以完整真实 `MapGenerator.GenerateMap(200×200, Settlement, AsteroidBasic)`生成内容，断言实际parent/tile/OrbitalDebris与**原矿物Mineable实体数>0**。该场景会真正触发nativeSpawnOres InvalidCast→MOD finalizer→真实lump/GenSpawn，不凭造一个异常对象说功能通过。
- 调实际注册GameComponent.FinalizeInit，断言MapParentAt实际定位Settlement且旧无map asteroid被移除。
- finally仅nativeDeinitAndRemoveMap自己临时地图/Remove自己Settlement、恢复原asteroid（如被产品移除）、矿物/设置/InitData/ProgramState/CurrentMap/WorldSelector层与选择。地图返回前清理，避免公共测试MapComponent再次执行。记录真实generation毫秒数；没有启动游戏进程或动用户存档。

检查尚未执行，状态adapted/runtime_pending；root确认该完整生成方案可在独立一次性用例执行，必要时由root调timeout。

## 未测/既有风险

轨道切换按钮/视觉操作、真正新开局后起始Pawn+初始装备/缺氧生存、Base_Player完整Orbit地图生成、surface↔asteroid来回切换、save/reload、gravship later landing、无Odyssey组合未覆盖。原算法会放行任何满足SpaceMapParent子类且在Orbit的对象，不限定仅BasicAsteroid；原stored mineral未自动清空，Base_Player选择也不显式重置已经设置的mapGeneratorDef，重复选点/重开局存在旧状态风险；尚未复现，记录而不扩展重构。无红字/编译通过不是完整开局可用证明。

## 真正地图生成发现的既有缺陷：v2

`mod64-rev1248-o`真实生成200×200 map/18个MineableGold/父对象去重通过（549ms），但新增两条ERROR，整体不通过，历史runtime保留。

1. `ScenPart_StartingAnimal.CanKeepPetTame` 对合法 startingPawns.MaxBy(...).skills 取值；旧测试设置0起始Pawn导致NRE。v2夹具设置1个真正生成的player Colonist及对应startingPossessions，保留完整native ScenParts，不删scenario/不吞异常；断言该Pawn实际出现在新地图有效可站立/无roof/Heavy地格。所有临时地图Pawn也在finally清理。
2. 产品已有Common/Patches/Patches.xml给SpaceMapGenerator插入FindPlayerStartSpot。原GenStep重建后CellFinderLoose要求District.TouchesMapEdge；实测`numTouch=1099`，小行星岛本就不连接地图边。它是现有产品缺陷，不能作为额外原版baseline错误忽略。

v2仅在新1.7增加 `GenStep_FindAsteroidStartSpot_Patch` Prefix：parent是玩家Settlement、generator精确AsteroidBasic、Space layer、非gravship、且没有已有PlayerStartSpot才进入。保留native RebuildAllRegionsAndRooms、真实Standable/unroofed/Heavy、district>=10、UsedRects/layout避让，只放宽陆地专用TouchesMapEdge前置，取最近中心合法地格设真实MapGenerator.PlayerStartSpot；未找到则返回native原逻辑，其他地图全走原方法，不吞失败。旧1.6及CommonXML保持。主代理明确认可该严格范围。

产品与v2检查均0/0；新日志 `logs/64-LandingOnAsteroid-build-v2.log`、`...-native-check-build-v2.log`。待主代理再跑完整生成，不以旧case的checks=true判成功。

### v3 只读定位材料

v2真实case `mod64-rev1248-q`仍有native central fallback (`numDistrict=867`, `numTouch=0`)且真实Pawn表面断言失败，尚不能判为适配功能通过。v3仅Tests具体observer记录start生成时parent/faction/generator/space/gravship/已设start/regionsEnabled及实际Standable/Heavy-unroofed/有district/CellCount>=10候选数量，失败消息展开Pawn的地图/坐标/terrain/Heavy。观察器无结果修改，finally移除自己的两个具体patch；生产保持v2，不屏蔽任何ERROR。v3检查0/0日志 `logs/64-LandingOnAsteroid-native-check-build-v3.log`，待主代理运行定位根因。

### v4 实际入口证据待测

`mod64-rev1248-v`通过真实Pawn/矿物断言但central error仍存在；两个已注册Generate观察器根本未进入，不能把patch注册当生效。v4仅记录loaded AsteroidBasic/Space的genSteps及真实CLR类名以核验XML继承/覆盖，没有修改生产或过滤日志；0/0，`logs/64-LandingOnAsteroid-native-check-build-v4.log`。若child未有实际step，需加入严格player Settlement限定的自有GenStep，避免盲目给普通太空地图增加陆地起点搜索。

### v5真实前置根因修复

`mod64-rev1248-x` loaded AsteroidBasic/Space列表明确都包含FindPlayerStartSpot，排除“child XML缺入口”推断，因此不新增XML/自有GenStep。真实guard全部合法且existingStart=false，**regionsEnabled=false**；native RegionAndRoomUpdater.RebuildAllRegionsAndRooms仅warn然后TryRebuild因disabled返回，无真实district可供新搜索。原版太空生成正常不需要起始Pawn，MOD加入起始点时必须补这个前置。

v5在既有限定Prefix内部保存Enabled、临时设true→原生Rebuild→同样真实合法候选查询，finally恢复原Enabled；仅恢复原功能所需前置，不修改其它地图/布局、不放宽其它validity、不吞失败。真实矿物/Pawn/all-ERROR门槛仍保留，product0/0，`logs/64-LandingOnAsteroid-build-v5.log`，待实际重跑。

v5检查额外保留地图后续路径门槛：真实落地Pawn对同district另一非自身地格调用nativeCanReach(OnCell,Danger.Deadly)，不是只验证找到start坐标；检查0/0，`logs/64-LandingOnAsteroid-native-check-build-v5.log`。原Enabled setter只是存bool，无清空grid副作用；临时true后finally恢复已核源码。


## 2026-10-15 名称和描述版本修订

本轮在现有1.7适配基础上更新About名称/描述版本措辞，packageId/发布ID/支持标签/生产源码与DLL不改。

- 原1.6开局限制作为背景保留；补明当前1.7适配，原操作/作者/语言链接正文完整保留。

语法/身份/版本声明与原产物一致已核验；属于元数据修改，不将其当新功能验证。详细回写审计见工作区expansion/metadata-changes.json。
