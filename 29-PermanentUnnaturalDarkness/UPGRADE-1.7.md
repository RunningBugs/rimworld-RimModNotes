> 主代理游戏验证：`mod29-rev1248-j`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod29-rev1248-j`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 升级：Permanent Darkness 无尽黑暗

packageId `RunningBugs.PermanentDarkness`；Workshop `3225237583`。原支持 1.5/1.6，本次基于当前 1.6 源码与五份 Def XML，固定游戏 `1.7.4977 rev1248` / `RW/17/Source`。状态 **已适配，生产与测试工程编译通过，实际功能等待主代理**。

## 改动与目录

新增独立 `1.7/Source`、`Defs`、同名 `Assemblies/PermanentDarkness.dll`；About 加 1.7。无活跃 LoadFolders.xml（只有 .bak），按真实默认加载精确 1.7 与根目录语言/Patches。旧版所有文件和备份不动；新 Def XML 原样复制，身份、引用、语言字段保持。

新版工程 net10.0，锁 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`；源码和新 DLL metadata 没有 Harmony/HugsLib 调用，移除新版工程未用的两个模板包；不需要 Publicizer。About 原本仅依赖 `Ludeon.RimWorld.Anomaly`，保持。

唯一生产逻辑改动：GameConditionTick 防重复检查由原版 `HediffDefOf.DarknessExposure` 改成实际添加的 `HediffDefs.PD_DarknessExposure`。原先检查 A、添加 B，且自有 TryMergeWith 返回 false，因此每60tick会再加一个 B并叠加伤害；这是一处已审查证实的旧缺陷，主代理确认修正仅限新1.7。伤害随机种类/区间、护甲忽略、阶段、通知与旧版本不改。

## API/功能语义

完整原/新静态 metadata 分别 `logs/29-PermanentUnnaturalDarkness/api-{original,new}.json`；六份 C#（QuestNode 全部注释）人工审查。游戏 Assembly-CSharp rev926→1248 全正文/API一致，引用 alpha1的游戏合同一致；未将纯编译当作游戏行为证据。

| 部分 | 真实 1.7 合同与路径 |
| --- | --- |
| GameCondition_ForceWeather / condition 生命周期 | Init、GameConditionTick、End、SkyTarget/LerpFactor、SkyOverlays、GameConditionDraw、ForcedWeather、ExposeData 均存在。Map/GameConditionManager.RegisterCondition 设置 owner/startTick并调用 Init；GameConditionManagerTick 调用未过期条件。自有 Permanent=true不自动结束。 |
| 全局 controller / MapComponent | 实际 GameComponentUtility/MapComponentUtility tick仍调用覆盖函数。GenTicks.IsTickInterval(250/60/300)、TicksGame、Start+Delay阶段、GrammarRequest/GrammarResolver、LetterStack.ReceiveLetter合同匹配。每地图自动补条件仍使用真实 ConditionIsActive(含Parent)和 MakeCondition/RegisterCondition。未使用的 targetWeather 局部变量保留，没有顺手重构。 |
| 天气/天空/图形 | WeatherDecider.ForcedWeather 枚举有效条件调用override；weather TransitionTo、sky eclipse colors、WeatherOverlay_UnnaturalDarkness和SkyTarget0glow保留。MapBrightness私有tracker经公开SetTargetBrightness改变；Material.color/MatBases.Darkness、DebugViewSettings.drawShadows存在，真实渲染和亮度过渡需可视验证。 |
| 暴露判断 | Pawn.Spawned/Humanlike/Downed/IsColonistPlayerControlled/IsColonySubhumanPlayerControlled及Held地图/位置、GetDoor/OccupiedRect/EdgeCells/GroundGlowAt合同保留。默认 mapbrightness≤0.01 且自有condition活跃，door边缘平均glow特殊路径维持。 |
| 自有 Hediff.Tick | 1.7原版Hediff_DarknessExposure把伤害放TickInterval，但本MOD原 Tick **并未失联**：Thing.DoTick→Pawn.Tick→Pawn_HealthTracker.HealthTick→Hediff.Tick仍每tick执行，同时DoTick另调用TickInterval；保留原override能维持原时间精度，不机械改接口。基类Tick空、基类TickInterval处理年龄。 |
| 伤害/通知 | Scratch/Bite/Cut、DamageInfo.SetIgnoreArmor、Pawn.TakeDamage.AssociateWithLog、BattleLogEntry_DamageTaken、DarknessDamage sound、通知判断、ShouldRemove/TryMergeWith/Notify_Downed均兼容。CheckNotifyPlayer仍查询原版 GameCondition_UnnaturalDarkness 而非本条件，故通常走原有Message分支；本轮不扩展通知设计。 |
| 保存/设置 | Controller六个PD.* key、settings三key、condition PD.anyColonistAttacked、Hediff nextDamageTick/lastNotifyTick原key不变。Mod/Listing/SliderLabeled/Math.Truncate纯辅助API不变。 |
| XML/DefOf | GameCondition/Incident/RulePack/两个Weather/Hediff同名类引用和DefOf字段匹配。原SolarFlare.silencedByConditions在Core真实XML存在，PatchOperationAdd目标有效；Rain与异常暗天音效在Core/Anomaly存在。 |

原实现的边界保留并列入后续：IncidentDef targetTags=World，world manager ownerMap=null，当前自有conditiontick只处理ownerMap；世界级incident路径不等于支持地图剧本路径，不能凭本次地图测试声称世界路径可用。End没有主动重置MapBrightness；图像过渡/新地图、保存后恢复需要验证。既有 PLAN 已列多地图问题，本轮不改全局结构。未把注释掉的 QuestNode 重新启用。

## 构建、有效单测、实际断言

```bash
scripts/dotnet.sh restore work/29-PermanentUnnaturalDarkness/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/29-PermanentUnnaturalDarkness/1.7/Source/mod.csproj -c Release --no-restore
scripts/dotnet.sh build logs/29-PermanentUnnaturalDarkness/Checks.csproj -c Release --no-restore
scripts/dotnet.sh logs/29-PermanentUnnaturalDarkness/bin/Release/net10.0/Unit.dll work/29-PermanentUnnaturalDarkness/1.7/Assemblies/PermanentDarkness.dll /home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies
```

生产、断言工程0warning/0error。纯辅助 `ModSettingsUI.IsMultipleOfPointFive` **已实际运行**编译DLL：201个 -10..10步进0.1 有符号值与NaN，均符合半步判定；BCL runner通过固定游戏程序集只解析类型，不启动游戏，不调用引擎。该辅助当前不在UI调用链，单测不代表UI或游戏功能通过。没有原有可执行tests。

`Tests/1.7/Mod29Checks.cs` 交主代理真实单MOD+Anomaly地图用例：

- 活跃加载 XML类、DefOf关联、SolarFlare实际补丁；真实组件/MakeCondition/RegisterCondition/Init永久激活，Delay合法范围，SkyTarget/overlay数据合同。
- 真实controller初期/警告/主阶段三信件，每阶段重tick不重复，阶段1/2 ForcedWeather，实际conditiontick请求zeroBrightness。
- 输入地图亮度tracker和skyGlow临时设确定黑暗值（仅测试，不冒充真实动画过渡）；真实dark-cell playerpawn，非生成/非玩家/nullmap/非interval/初期负控制；连续tick恰一份自有暴露；真实Pawn.DoTick安排480..720、实际受伤并180..300重排；Notify_Downed重置，亮处真实HealthTick清除。
- 实际RainMonitor.MapComponentTick配真实FireWatcher.fireDanger=100输入验证雨天安全路径；controller和settings实际Scribe；真实End全局关闭并恢复原Material颜色。
- finally恢复 tick、controller static、maptracker、sky/weather/fire/material/shadow/battlelog/letter；删除临时pawn并只移除本case给原有pawn添加的自有Hediff。所有文件只在隔离case；不触碰用户存档/Def库。

断言已编译，**尚未实际运行**。未覆盖：door平均光照/灯具/变种人与downed真实控制、伤害护甲统计/通知支路、settings渲染和阴影/覆盖/地图亮度真实渐变、所有地图与world incident、场景启动最早阶段、真实新进程保存重载和Hediff timer保存；本轮不能据此称无尽黑暗全功能已验证。
