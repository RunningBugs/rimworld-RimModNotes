# 天轴自用扩展1.7本地适配

2026-10-15；实际游戏1.7.4977 rev1248。包ID `RunningBugs.PolarisblocExt` 保持，无发布ID的完整自用工程，现由用户扩大范围授权。保留1.5/1.6、原始Defs/语言和内部存档键。状态：**prod/checks编译0warning/0error；游戏业务及新进程存档验证待root执行**。

## 最小变更与保留行为

新增独立1.7源/DLL，锁定net10、Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2。HugsLib只是模板引用，真实源码无其API/基类，因此仅1.7去掉；不需要Publicizer。显式PatchAll(typeof(Start).Assembly)。仍叫PolarisblocExt.dll，与版本加载路径覆盖规则一致。名称/描述增加1.7，About所声明CoreLab.tmp/WorkshopID/包ID未替换。

真实Core现在已有类似机会任务联系人，但41原有提醒与独立联系人/生成ItemStash/1500银费用/一年间隔全部保留，不合并、不空转。启用两者可能出现两种提醒/联系人，此属于已存在的双模块意图；未据此删原功能。原首请求也要求经过一年（last=-1但没有特殊首轮豁免），仍保留。

既有存档缺口：TechCoreGiver只被static singleton持有，虽然实现IExposable却没有深度序列化owner；请求冷却/联系人ID原来没有实际保存。1.7在原RequestTechCoreNotification.GameComponent的ExposeData中增加Scribe_Deep外层 `PolarisblocExt.TechCoreGiver`，仍调用原 `PolarisblocExt.TechCoreGiver.Id` / `PolarisblocExt.TechCoreGiver.lastTechCoreRequestTick` 内层键；原 `PolarisblocExt.RequestTechCoreNotification.sendTechCoreRequestReminder` 原样。GameComponent构造清除前一游戏singleton，避免新存档沿用上一个游戏对象。旧档没有原来未写出的历史冷却可恢复，missing新outer key时按原null/默认值重建；不伪造旧请求状态。

## 完整API/语义审查

源全部一文件（DefOf、GameComponent、IExposable/ILoadReferenceable/ICommunicable、Start、HarmonyPostfix）已审查。110静态成员/104类型引用见expansion/logs/41-PolarisblocExt/api-references.json；调用点/动态和业务链见api-review.json。没有假桩第三方接口；只引用游戏/Harmony，真实Core定义提供ResearchProjectDef/ThingDef。

- 原版1.7 `FloatMenuOptionProvider_FromThing.GetOptionsFor` 真正调用 clickedThing.GetFloatMenuOptions；`Building_CommsConsole.GetFloatMenuOptions(Pawn)` 仍有效，基类/CanUseCommsNow/实际GiveUseCommsJob入口都保留。补丁不是挂在不再执行的旧方法。实际代码RW/17/Source/RimWorld/FloatMenuOptionProvider_FromThing.cs:26、Building_CommsConsole.cs:94,117。
- DefOf需实际 `PolarisNanoRepairHazeGenerator` Research和Thing、vanilla `TechprofSubpersonaCore`；已定位本机Core1.7真Defs和最终Polarisbloc.dll。加载版本为本地 `.tmp` 包，不是工坊旧Core。Root必要运行包为Harmony+Vanya.Polarisbloc.CoreLab.tmp+41。
- Reminder保持研究完成/PlayerOrQuestRewardHas/core-or-generator缺失、每1999tick、一次提醒标志。Tale/收藏品/任务奖励扫描根据游戏实际PlayerItemAccessibilityUtility，声明动态game状态待真实测试。
- 联系人ID取UniqueIDsManager，调用标记、图标ContentFinder、commTarget job、Dialog_Negotiation radio/sound、Social disabled过滤均保存。comm菜单普通原结果完整yield回。
- 任务Slate points/askerIsNull/itemStashSingleThing、OpportunitySite_ItemStash.CanRun/GenerateQuestAndMakeAvailable、SendLetter、TradeUtility真实launchable银/LaunchThingsOfType、年度strict `>` gate都仍同意图。自动回根Dialog的latebind保持。没有改银价或合并Core的另一功能。
- Common/Defs/LetterDef.xml与Patches均空模板结构，实际语言键非模板，XML全部解析通过；无新Defs、更名存档字段或自有UI大改。

## 构建/证据及真实测试入口

```bash
bash scripts/dotnet.sh build expansion/work/41-PolarisblocExt/1.7/Source/mod.csproj -c Release
bash scripts/dotnet.sh build expansion/work/41-PolarisblocExt/Tests/1.7/checks.csproj -c Release
bash scripts/dotnet.sh tools/api-audit/bin/Release/net10.0/ApiAudit.dll expansion/work/41-PolarisblocExt/1.7/Assemblies/PolarisblocExt.dll
```

两者0warning/0error。15旧文件逐SHA保持，expansion/logs/41-PolarisblocExt/legacy-preservation.json。7文件manifest和最终DLL SHA见expansion/results/41-PolarisblocExt.json；deps.json、obj/bin、游戏/Harmony/测试DLL不应用。此mod没有可脱离game状态而有意义的独立纯业务函数；直接SDK执行reference DLL不能当游戏测试。

`Tests/1.7/Mod41Checks.cs`，namespace RW17.Validation、class Mod41Checks、真实PackageId，Run(Map)只用游戏/Harmony并反射真实本MOD：实际研究未完成/完成、生成core正阴条件、1999提醒和重复抑制、真实轨道交易信标与银、原版CommsConsole菜单→真实UseCommsConsole job.commTarget、执行请求→真正生成Site/科技核心reward/1500银减少/冷却、一年strict边界。finally恢复研究、tick、原字段/letters及仅新生成Quest/Site/fixtures，不影响用户存档；只在root一次性地图运行。

同一class公共 `SaveReload(Map,string mode)` 复用现有save/reload协议：实际整GameDataSaveLoader.SaveGame生成rw17-mod41-fixture.rws；rw17-save-state.json记录map ID、联系人ID、实际当前tick冷却和nondefault reminder=false。reload在新游戏进程检查真实rws的ID/冷却/提醒与availability；输出rw17-save-result.json/通用成功标记。没有只用独立Scribe XML冒充真实完整存档。Root负责route/串行运行，本代理未launch。

当前所有原生检查尚未执行；GUI谈话界面/交互、social-disabled/无电不可达条件、跨版本缺失旧singleton状态、第三方组合边界需分别记录。实际Job请求链及真实save-reload返回后才能标该范围通过。

实际整存档19.30秒/新进程回读15.40秒的4状态断言虽通过，`expansion-mod41-reload`仍有真实ContentFinder different-thread ERROR，因此未标验证通过。新Deep owner在后台Scribe实例化时触发原eager texture字段初始化；仅1.7改为lazy TechCoreIcon getter，真正CommFloatMenuOption GUI入口才访问ContentFinder，保留原图标，不用headless skip。需重跑新的全存档/回读。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-polaris41-ratkin43：18.0秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。
- expansion-mod41-save-v2：20.65秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。
- expansion-mod41-reload-v2：15.95秒，正常exit0/未超时，相关实际检查通过；无ERROR。

完成4项原生整存档新进程回读；后台深度构造延迟GUI纹理加载后无ERROR。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/41-PolarisblocExt。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
