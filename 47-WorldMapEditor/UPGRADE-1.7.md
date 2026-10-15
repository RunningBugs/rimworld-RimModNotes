> 主代理游戏验证：`mod47-rev1248-p`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod47-rev1248-p`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 升级：World Map Editor 世界地图编辑器

packageId `RunningBugs.WorldMapEditor`；Workshop `3438143106`。迁移当前1.6源码，以 `1.7.4977 rev1248` 固定Source/UnityStubs及真实世界对象合同为准。**已适配，生产与断言编译通过，实际数据场景待主代理；全世界规模/可视编辑未验证**。

## 必要最小改动

新增1.7源码/同名WorldMapEditor.dll、About1.7。默认自动1.7/Common/root；旧Common WorldObject/WorldGenStep XML与纹理/语言共享，.bak不作为活跃LoadFolders；旧源码和DLL字节保留。

- net10.0、精确 `Krafs.Rimworld.Ref 1.7.4977-alpha.1` / `Lib.Harmony 3.0.0-rc.2` / `Krafs.Publicizer 2.3.2`，去掉未用HugsLib及旧PublicizeAll；Publicizer仅 `RimWorld.Planet.SettlementAbandonUtility.AbandonCommandTex` 私有静态readonly图标，IgnoresAccessChecksTo策略。游戏原DLL不改、publicized引用不部署。
- PatchAll明确typeof(Start).Assembly；FactionDef原private factionIcon读取改官方FactionIcon getter（同一缓存，缺图官方BadTex）；Tile原private biome赋值改公开PrimaryBiome setter（真实实现仅赋同一字段）。
- Toggle注册从手动AllWorldObjects/privateHashSet AddRange改官方WorldObjectsHolder.Add；它同时更新layerWorldObjects缓存、SpawnSetup（renderer/FastTileFinder dirty）和PostAdd生命周期。旧hack不能登记1.7实际绘图层所需cache，即使HashSet声明Spawned也不等于完整注册。
- GenerateInvisibleWorldObjects先Clear生成cache；关闭CleanUp移除世界对象后Clear cache；GameComponent.FinalizeInit复用同一既有清理入口。避免worldgen和多次开关无限累加旧实例，并确保新游戏/加载后editor off。原逐地块隐形对象设计保留，不另建优化体系。

## API/语义审查

`logs/47-WorldMapEditor/api-{original,new}.json`为完整静态metadata，逐一核验全部Main函数/闭包与Def/Patch。游戏合同与rev1248真实相符，动态UI/性能不凭此判定。

| 路径 | 真实实现/判断 |
| --- | --- |
| WorldGenStep.GenerateFresh(string,PlanetLayer) | 仍正确override；其Def和WorldObjectDef均在Common。原步骤只按WorldGrid.TilesCount（Surface）创建对象，implicit int→PlanetTile指默认root surface；不扩展至轨道/地下层。FinalizeInit现在丢弃该预生成缓存，首次启用重新生成。 |
| PlaySettings.DoPlaySettingsGlobalControls postfix | 实际Map/World toolbar仍调用，WidgetRow.ToggleableIcon(ref bool,Texture2D,string,SoundDef,string)参数有效；仅worldView+Playing处理。不能借线程注释声称开启异步：实际逻辑仍在主线程同步执行。 |
| WorldObject registration/removal | Add更新list/hash/layer并SpawnSetup/PostAdd；Remove移除全部cache并PostRemove/Deselect。层WorldDrawLayer_WorldObjects.Regenerate实际读取AllWorldObjectsOnLayer，修复必要；world selection实际SelectableObjectsAt读holder.ObjectsAt。 |
| Save cleanup prefix | WorldObjectsHolder.ExposeData仍在world序列化调用；Saving时CleanUp关闭并移除所有隐形对象。InvisibleWorldObject.ExposeData原空override保持，因为发布对象在真正保存之前先移除，不把空override当可加载存档格式。 |
| 编辑gizmo/selection | WorldObjectCompProperties.compClass/WorldObjectComp.GetGizmos；真实selected InvisibleWorldObject过滤；WorldSelector原80对象限制保留。SettlementAt/MakeWorldObject/SetFaction/SettlementNameGenerator/Add/Remove均匹配，不改当前有地图定居点的危险删除规则（只在新无地图对象测）。 |
| Tile data editing | PrimaryBiome公开setter和hilliness字段；WorldRenderer.Notify_StaticWorldObjectPosChanged/RegenerateAllLayersNow仍可用。当前操作仍逐选中对象全层同步刷新，不顺手优化；HillinessLabel私有缓存等派生信息未主动失效，需UI/重载专门检查，不能由字段变化证明全体派生显示正确。 |
| Faction/texture | FactionIcon getter替代privatefield，其颜色、Name/Hidden、faction manager等一致。原菜单会补空FactionDef.settlementTexturePath和设置全局Settlement Def.texture=BadTexPath，既有全局纹理副作用保留并注明，driver恢复副作用。 |
| invisible Material | MaterialPool/ShaderDatabase.WorldOverlayTransparentLit/WorldMaterials queue合法；shared Graphics RenderTexture复制问题与该普通世界物体material路径不同，没有机械改CPU纹理。shader/透明可选取/世界layer视觉待可视检查。 |

## 编译/数据断言与限制

```bash
scripts/dotnet.sh restore work/47-WorldMapEditor/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/47-WorldMapEditor/1.7/Source/mod.csproj -c Release --no-restore
scripts/dotnet.sh build logs/47-WorldMapEditor/Checks.csproj -c Release --no-restore
```

生产与断言0warning/0error，输出只本MOD DLL/deps.json。静态XML/旧字节/加载和输出检查见 `static-validation.json`。没有现成单测；全部自有函数依赖当前World/对象/UI/游戏Scribe，没有脱离环境的纯业务逻辑，不制造替代世界模型的复刻单测。

`Tests/1.7/Mod47Checks.cs`已编译，尚未执行，真实场景：

- 实际FinalizeInit已清cache/off状态；真实PlaySettings patched入口（测试仅阻止无关原toolbar body，通过特定WidgetRow受控输入驱动原Postfix）。Map toolbar阴性。
- 真实每次全Surface生成函数均实际执行并先断言数量==TilesCount/unique tile/未注册。测试postfix随后仅保留32个生成对象作为**有界注册输入**；真实官方Add未拦截，观察本对象nativeSpawnSetup/PostAdd各32次、Spawned/layer cache/原生SelectableObjectsAt；实际关闭移除全部且不影响原世界对象，重复开关产生新实例不累积。该方式验证生命周期和重复数据，不冒充全人口注册性能。
- 实际selected对象四gizmo，真实FloatMenu选项Action修改PrimaryBiome/hilliness并观察实际刷新请求（GPU regeneration仅测试阻止）；真实无地图Settlement生成/层cache/faction与销毁；真正WorldObjectsHolder.ExposeData保存调用的原Prefix移除对象/模式/缓存，并验证实际XML没有隐形对象。
- finally精确移除测试patch、新worldobjects/windows，恢复tile biome/hill、Faction/Settlement纹理字段、selection和原staticcache；仅在隔离case写Mod47WorldFixture.xml，不修改正式存档。

**剩余未验证**：完整Surface对象的注册/关闭性能、实际世界map拖拽/鼠标80对象限制与透明像素、地块所有派生cache/渲染和真实后续地图生成/世界pathing、轨道层、已有带地图定居点与任务、真正保存新进程加载以及多MOD组合。官方Add中list.Contains使全世界批量加入可能N²，原模组已经公开提示大量对象的性能问题，本轮数据断言没有测完整注册时长。不能以32个对象功能通过称大世界不卡顿。
