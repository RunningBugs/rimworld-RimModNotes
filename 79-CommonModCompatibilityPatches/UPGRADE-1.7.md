# Common Mod Compatibility Patches：1.7本地适配

用户扩大范围授权的完整未发布自用项目。真实游戏1.7.4977 rev1248；全部30组原有业务源码逐字节复制到新1.7，未删组/改功能/保留推测fallback。新产物SHA256 3374b60c038ce61dafbb47193cb1f81029b7269d6947bbaca27fd13a9a1a9c06。旧1.6源码/DLL保持，仅About/LoadFolders/AGENTS说明改变。

独立net10.0 +固定Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2、Krafs.Publicizer2.3.2；从旧全公开收窄到RimWorld.ListerHaulables.groupCycleIndex/cellCycleIndices，IgnoresAccessChecksTo运行机制。当前真实1.7仍用私有static游标、nativeTick实际递增，保护有必要；其他直接引用不需publicize。原逐组Harmony.Patch及owner不变，不添加PatchAll。输出仅自有CommonModCompatibilityPatches.dll，排除Game/H3/ref/Test/bin/obj/deps。

全303静态成员/195types和所有30组的反射目标/参数名/可见性/字段/继承/动态入口/原生语义/H3参数注入/IL假设见expansion/logs/79-CommonModCompatibilityPatches/api-review.md、groups-review.json与api-references.json。当前正确选择的32DLL合同actual-contracts-current.jsonl、installed-dependencies-current.json与rootnativeboot路径/哈希对应；精确反编译actual-current.cs/actual-1.7.cs为语义证据。Kiiro旧CompPostTick仍通过Thing.DoTick->Pawn.Tick->HealthTick->HediffWithComps.PostTick实际调用，同时保留newInterval，不因新接口出现而误判无效。VideoPlayer新fileURI经Uri.LocalPath解码，与现有VBE转义相容，实际视频播放另测。

构建生产及Tests/1.7/checks.csproj均0warnings/0errors。命令bash scripts/dotnet.sh restore/build expansion/work/79-CommonModCompatibilityPatches/1.7/Source/mod.csproj（固定.nuget/nuget.config；Release --no-restore），checks同策略。10实际最终DLL纯逻辑回归passed：7URI空/相对/远程/已URI/空格#Unicode边界，3cursor复制/清空/源不别名；unit.log，无gamebootstrap/patching/native输入。旧whitebox29结构检查过（legacy-whitebox.log），仅读旧1.6和metadata，不当新版功能证明。

实际Map/H3场景Mod79Checks.Run不编译依赖MOD。按activegates要求本组所有具体targets注册（Horax/VFE2、RimStory2、LTO5等），inactive实际TryApply=false+无patch，只记真实缺失而非功能成功。核心：lifeStage存在/缺失、bed已分配/满员、真实ChooseNextSong零权重recent回退（所有SongDef字段finally复原）、两个真实Lister实例交错游标、真实shuttlewait同Map留与maplessSendAway、真donor/copy跨基因表override、原生psychicallysensitive invoker.TryAssign/Horax库存3of5安全转移与精确消费。

有限optional：真正AllowTool v1.6 cache-provider的有效候选不能被清空，deSpawn阴性；真实ReplaceStuff1.7 GetNeededBridge(TerrainDef)非法/正常groundnull、WoodWall/Mud真Bridge正控；真实5参数overMineable非法安全+GenConstruct.BlueprintForBuild于Granite触发原Mine，并由原生WorkGiver_Miner找回同一岩石；本机QualityBuilder v1.6 nullMap；HAR实际nestedpostfixnull/outervalidGrammar；VBE真实getterURI roundtrip（不假报播放）；InventoryTab实际Slot相同市值稳定比较。所有fixturefinally清自己Thing/zone/terrain/shipmanager/游标，不用用户地图/存档。driver compile不是功能通过；runtime更新以工作区results为准。

主代理实际core-v2运行16.84秒通过，错误仅原版0x0分辨率基线；optional-v2运行19.55秒到岩石创建时失败，原因是夹具误用不存在的MineableGranite。现改用官方ThingDefOf.Granite（UglyRockBase→RockBase→Mineable），并按1.7的Mine.targetType=Cell使用DesignationAt；原DesignationOn查询会主动记录错误，不是合法正控。蓝图与采矿标记的断言保留，增加真实原生采矿provider确认。十四项Def字面量/DefOf字段在当前官方XML预检全存在（literal-defs-preflight.json），新版夹具编译0/0；后续实际结果记录如下，生产源码及DLL未改变。

optional-v3在AllowTool新标记的有效候选断言失败。真实v1.6原生Designator_HaulUrgently.DesignateThing在ToggleDesignation/解除禁止后调用HaulUrgentlyCache.ClearCacheForMap；缓存有效期为1秒，HugsLib FixedUpdate/alert可在本帧预先缓存空列表。夹具此前直接AddDesignation绕过了这个通知，不能把同帧旧缓存当作产品丢候选。v4夹具保留原正控，并记录之前无标记、直接标记无通知、真实工具标记、移除自有具体Prefix后的原生provider、恢复后provider、despawn阴性六阶段实际诊断；输出rw17-mod79-allowtool-diagnostic.json，含缓存/候选ID、Spawned/holder/map/标记/lister/实际安全filter。仅移除本MOD的该Prefix，finally以原owner/priority/before/after恢复，不改变其它补丁。强断言真实lister和cache包含、原生与恢复后的provider都包含同一钢材；finally删除自身标记并按原生方法清缓存。诊断不是功能通过，v4待主代理真实运行；生产仍未改。

过程审查纠正：首次explicitLoadFolders模拟错误把前列XML路径当优先路径，实际原生descending选择后列，所以审到了旧rootAllowTool与ReplaceStuff1.1。actualboot证明真正v1.6/1.7接口本来匹配原79，所有不必要3backend推测修改已撤销。此前失败为随机invoker无PsychicSensitivity或错误接口fixture；修为原生有效条件和准确真实target，没有放宽正控/隐藏日志。详情loadfolder-selection-correction.json；历史错误选择artifact只作过程记录，不作当前结论。

仅TinyTweaks目标包当前确未安装，保留完整组和absence负控；其它已安装目标未以“optionalmissing”笼统skip。仍未全面覆盖的groupworkflow分别记录groups-review.json：RimStory周年/葬礼、NewRatkin pool、Kiiro完整tick、GD3groundAoE、PawnHealthBar .01与UI、Minify占用床重装、VFE并发、DynamicPortraitoverlay、LTO创建/转换/reload/账单组、真正webm/GPU、双Map存储全流程、shuttle飞离/存档迁移/任意组合。不宣称目标登记或无红字代表完整功能。

About双语30组列表保留，新增1.6/1.7当前说明；中性标题不强加tag。新LoadFolders v1.7根+1.7、旧layout不动；AGENTS保留双语列表/loadAfter惯例，用户不允许自动commit/push覆盖旧自动commit条款。仅stage/独占results/logs，未修改真实仓库、工坊、sharedharness或启动游戏；root串行测试/受控回写。


## 主代理最终验证（2026-10-15）

expansion-common79-core-v2：16.84秒，8 active组/9真实targets注册与核心业务断言通过，22 inactive组实际TryApply负控不算功能pass。expansion-common79-optional-v4：20.30秒，实际AllowTool/HugsLib、ReplaceStuff、QualityBuilder本地fork组合业务检查通过；全部正常exit0/未超时，只存在原版0x0 resolution基线ERROR。

有限组合覆盖真实Designator清缓存、原生未patch/恢复后的provider均保留同一有效物品、despawn负控、真实桥/矿石蓝图采矿及nullmap。原业务源码与1.6字节一致；1.7固定新Ref/H3及两个ListerHaulables游标的精准Publicizer。早先静态依赖loader模型优先级有误，实际boot纠正后撤销了无必要的fallback/API业务修改；这不是Steam更新。测试不再固定错误方法形状、猜测Def名或绕过缓存失效通知。

仅所列真实业务/目标注册通过，不宣称所有30组全部workflow/视频播放/物理UI/任意第三方组合通过。root实际脚本/boothashes/日志/各30组ledger均保存在testing/cases及expansion/logs。完整备份见expansion/backup.json，旧版/ID保留；本轮不提交推送。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/79-CommonModCompatibilityPatches。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。

## Better Pawn Control 任务离开空引用修复（2026-11-16）

- 环境：实际游戏 `1.7.5005 rev1082`，当前启用工坊 `VouLT.BetterPawnControl`（1541460369）的 `v1.6/Assemblies/BetterPawnControl.dll`，并非本地 fork。
- 根因：失效人物引用留下 `AssignLink.colonist == null`。BPC 的 `Pawn_GuestTracker_SetGuestStatus.Postfix` 调用 `Pawn.Equals(Pawn)`，1.7 实现不接受 null，打断 `QuestPart_Leave -> MakePawnsLeave -> MakePawnLeave -> SetFaction -> SetGuestStatus`。
- 新增第 31 组 `BetterPawnControlStaleAssignLinksCompatibility`：动态检测原版/fork 包和实际类型、字段及方法；在原生 `Pawn_GuestTracker.SetGuestStatus(Faction, GuestStatus)` 的 prefix 清理空人物缓存，在 BPC `DataStorage.ExposeData()` 的 PostLoadInit postfix 清理读档遗留。LoadingVars/ResolvingCrossRefs/Saving 不清理，避免误删尚未解析的跨地图引用。只删除空记录或空 colonist，不清空列表、不改有效策略、不禁用 BPC 原回调，也不全局修改 Pawn.Equals。
- 保留旧 1.6 源码/DLL 字节；About 双语说明和两个 BPC loadAfter 条目同步更新。生产项目仍使用原有固定 Ref/Harmony/Publicizer，Release 构建 0 warnings / 0 errors。新 DLL SHA256：`dd4c0bcc2ecf9fbad4eecebae0ad928a307698b88ad73cbe8a5ad96745993d13`。
- `Tests/BetterPawnControl` 使用实际游戏和工坊 DLL、独立 Mods/SaveData/XDG 目录执行真实进程。active-v2 的 16 项断言通过：先用未改工坊回调复现异常，再验证真实 ExposeData 的 PostLoadInit 清理、早期读档不清理、有效策略对象/顺序/内容不变、原默认策略分支继续执行、重复调用幂等，以及真实已接受 Quest 的离开信号最终创建 `LordJob_ExitMapBest`。absent-v1 的 4 项断言确认无 BPC 程序集时无注册且 TryApply 返回 false。两次都正常退出、无 ERROR；32 项静态白盒检查通过。
- 首次 active-v1 测试夹具未接受任务，且信号未使用 `Quest{id}.` 前缀，被原版正确过滤；修正夹具为真实 accepted 状态和合法任务信号后通过，未放宽退出 Lord 的业务断言，生产代码未因该测试失败而变化。
- 测试和备份：`/tmp/opencode/rw-bpc-quest-fix-20261116/`，含 before-sha256、active-v1/v2、absent-v1、构建产物及日志。未宣称已覆盖用户全部 Mod 组合或整个实际存档的运行。
- 用户另要求直接修好存档：从最新 `赤裸1020#§#秘密花园-奥伦-3.rws` 另存 `赤裸1020#§#秘密花园-奥伦-3-季辉任务已修复.rws`。仅删除一条空人物 AssignLink，并把 Quest77 已到期的离开 Delay 从 Disabled 恢复 Enabled；不改截止时间、不强制成功、不推进/回滚时间。其余 XML 语义整体一致，原存档哈希不变；报告为备份目录中的 `save-repair-report.json`。
