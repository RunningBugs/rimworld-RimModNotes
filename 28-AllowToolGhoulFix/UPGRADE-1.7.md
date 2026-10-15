> 主代理游戏验证：`mod28-rev1248-j`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod28-rev1248-j`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**纯 XML 适配完成，实际上游与 Ghoul 场景待验证**。此 MOD 实际没有发布 DLL；1.4/1.5 Source 中的 C# 只是未构建日志模板。最小更新仅 About 新增1.7及仅v1.7上游loadAfter，不强行增加无功能 DLL/依赖。Common XML 和全部旧内容逐字不变，默认加载 Common 原补丁。

## XML/API 实证

实际 Allow Tool / packageId unlimitedhugs.allowtool（Workshop761421485）仍选择 v1.6+root，真实 root Defs/WorkTypeDefs/AllowToolWorkTypes.xml 中 FinishingOff.relevantSkills 唯一目标确实含 Melee（29）；原 PatchOperationReplace MayRequire 的XPath命中恰好1项并替为空列表。解析与数量证据 `xml-review.json`。WorkTypeDef.relevantSkills、SkillRecord.TotallyDisabled、SkillDef.IsDisabled、Ghoul mutant/skill初始化合同在当前游戏存在；没有Harmony补丁、反射或自有存档字段需要迁移。

**实际原bug机制在1.7已由原版预防**：Core SkillDefs 的 Melee 明确 neverDisabledBasedOnWorkTypes=true；Source/RimWorld/SkillDef.IsDisabled 在worktype检查前对此返回false。因此即使上游旧列表重新含Melee，Ghoul也不该由于FinishingOff工作禁用而禁用近战。保留原补丁是兼容处理，不夸称本次新增修复；场景不人为关闭原版保护来制造回归。

实际递归依赖 brrainz.harmony、unlimitedhugs.hugslib（Workshop818773962）、unlimitedhugs.allowtool。上游真的使用HugsLib，所以不能机械删依赖；它们本机标签到1.6，actual1.7baseline待root，旧标签本身不判不兼容。DLL来源/哈希在 dependencies.json。HugsLib root/Assemblies另捆绑旧0Harmony.dll，root测试须核验实际Harmony3加载，不可引入第二不匹配Harmony，也不得改Workshop。

## 验证范围

没有生产C#编译步骤或虚假api-audit；build.log明确N/A。Tests/1.7/Mod28Checks.cs 使用精准Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2/net10.0编译0warning/error，只作为测试驱动、不加载模板DLL。

场景要求真实Anomaly：实际已加载FinishingOff相关技能清空、生成真实Ghoul Pawn、真实Melee Skill不TotallyDisabled、真正meleeVerbs.TryGetMeleeVerb/Available可用；再暂时还原上游原Melee关联，刷新实际skillcache，验证当前原版保护仍保持melee可用；finally恢复空列表、清理pawn/RNG。这是实际XML/技能/攻击verb回归，记录原版已覆盖原因，未运行游戏。独立无意义镜像单元测试不增加。

待root先验证AllowTool/HugsLib 1.7基线，再运行场景；完整手动近战命中/伤害、AllowToolFinishOff实际job、Ghoul不同状态、DLC组合和其他技能/工作MOD交互待测。只清列表和没有红字不能代替真实Ghoul功能通过；真实存档/用户环境未改。
