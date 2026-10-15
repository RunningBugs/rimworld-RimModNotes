# 无尽黑暗饥荒扩展：1.7归档导出

2026-10-15，实际游戏1.7.4977 rev1248。完整归档由用户扩大范围明确授权导出为可用根包，不因为没有发布ID/曾封存而跳过。当前**编译通过，原生业务/存档/渲染仍待root执行**。

## 实际基线与加载布局

采用真正 `backup/1.6/Source` +实际`backup/1.6/Assemblies/PDEDontStarve.dll`+ `backup/Common`配套版本。原暂存曾误选backup/1.5 Source，其CompProperties_HasLightBulb.lightDef是ThingDef且要求wrapper CompGlower，而Common LanternGlowFleck实际是FleckDef、wrapper无CompGlower；该混配拒绝保留。1.6实际DLL/Source都为FleckDef/装备Pawn FleckMaker，证据expansion/logs/32-PermanentDarknessExtendedDontStarve/legacy-light-decompiled.cs。实际DLL有额外UpdateGroundGlow方法，但对于装备wrapper的Map=null无有效dirty操作，不把其方法名当移动GlowGrid来源。

恢复的新根About/Preview/LoadFolders仅声明和加载1.7（根+1.7），全部旧1.5/1.6/About/README/source/DLL/texture28文件逐SHA保持，旧包仍封存在backup。顶级原来没有About不可加载，不杜撰新的旧版本加载器或修旧历史混配。新增1.7独立Source/Assemblies/Defs/Patches/Languages/Textures，主代理按manifest受控回写，不部署旧归档内部文件。

使用net10、固定Krafs.Rimworld.Ref1.7.4977-alpha.1。真正4个源文件没有Harmony API/补丁/HugsLib；去掉新工程暂存模板Lib.Harmony及HugsLib，不添加无需要的Publicizer。测试观察器引用真实Harmony3，产品不携带HarmonyDLL/测试DLL/引用包/deps.json/obj/bin。

## 完整API、tick与行为审查

四源文件涵盖GenStep/FireFly/CompGlower/plantable+validator、LanternWeapon/Minified/portableFleck/fuel/gizmo/optionalhediff、DefOf与DebugAction；258成员/160类型引用完整audit及动态/语义文档在expansion/logs/32-PermanentDarknessExtendedDontStarve/。

- **1.7原生owner递归Tick是真正入口。** Thing.DoTick先正常Tick/TickInterval，然后遍历IThingHolder自有/child holder→各ThingOwner.DoTick。Pawn.GetChildHolders包含equipment/apparel/inventory/carry，LanternWeapon继承MinifiedThing持有innerOwner，内层Building_Lantern再次正常Tick其comps。读RW/17/Source/Verse/Thing.cs:700-772、Pawn.cs:4699、Pawn_EquipmentTracker.cs:372、RimWorld/MinifiedThing.cs:135及ThingOwner.DoTick。配套1.6 Source本身已经没有旧1.5手动innerComp/Primary.Tick；不硬移植旧Mono tick hack，也不调用protected Tick以双调。
- 当前RootHasLightBulb契约是FleckDef→LanternGlowFleck。装备/衣物owner的Pawn.Map存在才FleckMaker.Static(Pawn.DrawPos)；短life0.12秒由真实FleckSystemBase/Static.TimeInterval处理，不创建Thing灯光泄漏。Native绘制和GlowGrid光照不是同一证据，不能以Fleck创建当照明通过。
- 内层燃料CompPausibleRefuelable覆写CompTick只按实际IThingGlower条件消耗fuelConsumptionRate/60000；原IThingGlower只看燃料，AllComps循环不递归自调用。原wrapper携带Fleck并未按InnerThing燃料过滤，地面packed wrapper不发portableFleck；原已展开Building有CompGlower/燃料。这些既有1.6语义保留并记录，不假称燃料耗尽后携带Fleck会停或地面packed物也具完整照明。
- FirefliesSeed XML当前真正绑定**原版CompPlantable**，customCompPlantableFireFly只在注释中、私有plantCells shadow不活跃，不能为测试伪造换绑。原版DoPlant负责种植、sown/growth/history/PlantsSown并消耗种子；source接口现仍存在。
- 实际PDE_FireFlies GenStepDef经PatchOperationAdd加入Base_Player.genSteps；Density/SeedPart、CanSpawnAt room/spacing/edge/artificial/special-validator、baseTrySpawnAt/Wipe/growth保留。MapInitializing的Room/Fog/Reachability存在前提，真实生成待native，不仅编译。
- CompGlowerFireFly附近真人每60hash tick dim，100tick冷却后恢复；CompGlower.UpdateLit真实地图glowgrid回调。原lastDimTick、glowing Scribe键，optionalLightSourceHediffComp的PDE.wornPrimary以及vanilla plantCells/fuel/equipment/innerContainer都保持。未启用optionalhediff注释或新增存档架构。
- validator对DLC Def都先检查ModsConfig；DefOf Plant_Fireflies/Building_Lantern、FirefliesSeed/Texture路径对应；XML全部解析。DebugAction DarkTheMap保留但不会自动触发，GUI fuelbar/target overlay无窗口不能证明绘制。

## 构建与原生测试准备

```bash
bash scripts/dotnet.sh build expansion/work/32-PermanentDarknessExtendedDontStarve/1.7/Source/mod.csproj -c Release
bash scripts/dotnet.sh build expansion/work/32-PermanentDarknessExtendedDontStarve/Tests/1.7/checks.csproj -c Release
bash scripts/dotnet.sh tools/api-audit/bin/Release/net10.0/ApiAudit.dll expansion/work/32-PermanentDarknessExtendedDontStarve/1.7/Assemblies/PDEDontStarve.dll
```

prod1个原unusedCompGlower warning/0error；checks0warning/0error。DLL SHA、21文件manifest、原归档28文件校验见expansion/results/32-PermanentDarknessExtendedDontStarve.json；此代理没有运行游戏。

`Tests/1.7/Mod32Checks.cs`的RW17.Validation.Mod32Checks.Run(Map)：真实Pawn.equipment→Minified inner持有链，Harmony仅观察实际inner fuelComp/FleckManager.CreateFleck，Pawn.DoTick一次应仅调用1次fuelComp且实际fuel差匹配；移动后的x/z灯光位置、地面packed无portableFleck、actual FleckSystem data出现并经过其正常Tick/Update到期移除；真正新Map已有Base_Player fireflies与进一步GenStep.Generate；真实种子CompPlantable.DoPlant→plant/record/seed消费、附近人dim/远离cooldown恢复。Observer finally只取消自己的两个PatchMethod，恢复tick并销毁自有临时objects/植物，不UnpatchAll。

公共SaveReload(Map,string)复用root协议，整rws保存/新进程核对equipment及innerID/owner、非默认fuel0.375、seed plantCells、fireflylastDimTick+glowing，输出rw17-save-state/result与通用marker。Optional未绑定PDE.wornPrimary保持字节但不伪造可达业务。

限定未测：真实执行所有native和完整新进程save；GUI携带Fleck/展开Building照明/装置样式/血量fuelbar/targeting、GlowGrid实际光照、旧1.5/1.6跨版存档与任意DLC/第三方组合、旧unusedhelper路径。根包只支持真实适配1.7，原归档没有被升级或覆盖。

首次真实case expansion-darkness32-17-a中32/29全部业务断言通过，但2新增textureERROR使本轮不能verified。LanternGlow.png原档/新副本都是0字节，不是错扩展可重命名；真实配套Fleck引用原版Things/Mote/FireGlow，全newSource/Defs没有引用PDEDontStarve/Mote/LanternGlow。只从新1.7导出剔除这一未引用空占位以避全目录prefetch错误，旧backup文件完全未改、没生成替代图。manifest21文件，资源证据asset-fix.json；需新case重新加载验证。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-darkness32-17-b：18.1秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。
- expansion-mod32-save-v2：19.05秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。
- expansion-mod32-reload-v2：15.14秒，正常exit0/未超时，相关实际检查通过；无ERROR。

完成5项原生整存档新进程回读；已剔除新导出中未引用0字节占位，旧backup保留。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/32-PermanentDarknessExtendedDontStarve。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
