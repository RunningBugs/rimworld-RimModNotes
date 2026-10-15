> 主代理游戏验证：`mod51-rev1248-p`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod51-rev1248-p`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**适配/编译完成，实际三类诅咒和设置回归待验证**。保留1.5/Common/旧DLL、packageId/发布ID；新增独立1.7/net10.0、About支持。精准Krafs.Rimworld.Ref1.7.4977-alpha.1/Lib.Harmony3.0.0-rc.2，显式PatchAll(typeof(ModSettingsUI).Assembly)。实际patch类和MemoryThoughtHandler.pawn现均公有，所以新工程去掉原PublicizeAll和Publicizer整体包、不扩大访问；同时去掉未用HugsLib模板。旧版本保持。

## 实际API/语义

当前真实RatkinAnomaly 1.6 DLL固定参考Private=false，不进入Assemblies，来源/sha见dependencies.json。游戏1.7MemoryThoughtHandler.TryGainMemory(Thought_Memory,Pawn)、Pawn.Kill(DamageInfo?,Hediff)和Pawn.PostApplyDamage(DamageInfo,float)仍被运行调用。实际三个上游patch方法参数未变：AteWithoutTable_Patch的名Postfix实际标[HarmonyPrefix]、KillAnimals/Wounded两Prefix；本MOD对这些**上游patch方法本身**安装Prefix，以配置/IsColonist决定是否继续诅咒逻辑，不能把方法名当成执行时机。选定反编译与RAUtility.IfCurseActive读取World.RAComponent.curse的真实链已留logs。Harmony3的patch-on-patch实际生效仍须游戏断言，而非仅注册。

原功能全部保留：enablePatch默认true且Scribe键enablePatch不变，配置关时三类效果恢复上游；设置UICheckbox/WriteSettings/翻译Common仍兼容，private Targets用实际公有类型AccessTools匹配，无桩。全部静态API audit见api-references.json，Def/语言与存档字段不重命名。

**现有语义限制原样记录**：KillAnimals拦截的Pawn __0是被杀动物，非damage instigator。故enablePatch=true会抑制所有动物触发Chimera（动物从不IsColonist），不是“仅玩家杀动物时生效”。这是原实现既有行为；本轮不顺手改成killer检查。Meal/Wounded对象是受效果Pawn，则按真实IsColonist（非所有Faction.IsPlayer成员/奴隶）限制。None/mutants/slaves/动物细节不能按产品描述推测。

## 构建、场景和未测范围

production build.log和checks-build.log均0warning/error。命令 `bash scripts/dotnet.sh build work/51-RACursePatch/1.7/Source/mod.csproj -c Release --no-restore`。没有独立值得脱离游戏生命周期测试的算法，不制造机械Prefix镜像unit。

Tests/1.7/Mod51Checks只game/Harmony，实际外部assembly反射读取三目标/设置和World component。有效colony/outsider Pawn通过真正MemoryThoughtHandler.TryGainMemory触发meal诅咒：设置开out不FoodPoisoning、colony有FoodPoisoning，关后out有；两fresh有效Pawn的真实TakeDamage触发Wounded：开out不Downed，colonyDowned且不Dead，关后out同样Downed且不Dead；两个真实Cow的Pawn.Kill触发KillAnimals：开不新增Chimera，关新增一只真实Chimera。finally还原设置/curse/RNG并清理生成Pawn/Corpse/新Chimera。通过真正game调用链而非直接运行自己的Prefix；尚未运行游戏。

root实际运行递归IDs brrainz.harmony、erdelf.humanoidalienraces（839005762）、solaris.ratkinracemod（1578693166）、fxz.ratkinanomaly.update（3293914637），官方Anomaly必要，已安装上游均声明到1.6需先baseline（旧标签不证明不兼容）。RatkinAnomaly LoadFolders里的MeleeAnimation可选路径不要无故启用。待设置UI、保存/重载Scribe、第三方Harmony顺序、其他curse/animal/mutant/slave/questPawn场景验证；也需考虑整旧HAR/Ratkin/Anomaly上游1.7行为。未改Workshop、用户存档、系统环境。
