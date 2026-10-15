# RPP_Bill_AllMech_Patch：1.7调查记录

状态：**blocked，未升级**。只添加此记录；原源码、1.4 DLL、About及支持版本未改，未制造1.7标签/假依赖桩。

游戏1.7.4977 rev926；packageId RunningBugs.RppBillAllMechPatch；发布ID3108500485。关键运行依赖 alaestor.miscrobots.plusplus（Workshop747645520）在游戏Mods和Workshop顶层有效About中均未发现，ID目录本身不存在；没有已安装RPP机器人定义或实际上游代码可核验。仅Common Sense被检出，与所缺目标不是同一MOD。证据logs/07-RPP_Bill_AllMech_Patch/dependencies.json。

源码的唯一Harmony目标Bill.PawnAllowedToStartAnew(Pawn)在1.7仍公开virtual bool（RW/17/Source/RimWorld/Bill.cs:234）；PawnRestriction/SlavesOnly/MechsOnly、Pawn.IsColonyMech/IsColonyMechPlayerControlled/IsSlave、RaceProps.mechFixedSkillLevel、skills.GetSkill().Level、RecipeDef.workSkill/mechanitorOnlyRecipe、allowedSkillRange min/max、MechanitorUtility.IsMechanitor、JobFailReason.Is/Translate及Logger入口仍在本次真实源码/引用环境中。Postfix仅针对机器人defName以RPP_Bot_开头、任意机械体筛选而未设个人/奴隶约束时替换结果，并重新执行技能/机械师限制。当前新增的NonMechsOnly原版分支不由旧补丁覆盖；不能只凭Bill接口存在认定RPP中bot仍满足这些种族/归属规则。

启动已显式PatchAll(Assembly.GetExecutingAssembly())。没有直接第三方DLL调用、反射私有目标或transpiler；但实际bot数据与上游工作/派系语义是功能前提。HugsLib旧模板编译引用在源码无实际符号使用，但本次未生成1.7工程，旧工程也未顺手改。

已有1.4产物的完整静态引用审查保存在logs/07-RPP_Bill_AllMech_Patch/api-references-baseline.json；它只描述旧基线，不是1.7编译证据。未运行单元或游戏测试，build.log明确SKIPPED。没有新增DLL/1.7支持、没有修改或安装第三方MOD。

恢复条件：提供实际可用于本机1.7的Misc Robots++及基础依赖，核验RPP_Bot_定义/IsColonyMech与受控状态；随后新增独立net10工程并核验Bill实际入口、限制条件、真实机器人生成/清单按钮与工作签发。本次记录不能混入已适配列表。
