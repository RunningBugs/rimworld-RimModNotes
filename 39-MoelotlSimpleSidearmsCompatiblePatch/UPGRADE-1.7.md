> 主代理游戏验证：`mod39-rev1248-o`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod39-rev1248-o`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**已适配/编译，实际上游和物品查询回归待运行**。保留原1.5/Common/DLL/ID；独立1.7和About支持。net10.0、精准Krafs.Rimworld.Ref1.7.4977-alpha.1/Lib.Harmony3.0.0-rc.2、显式程序集PatchAll；未使用HugsLib模板删除，无私有直接编译访问/无需Publicizer。新工程PathHint拼错改HintPath、去Version=*，固定**实际安装**Axolotl.dll参考Private=false，不发布第三方引用。

## 实际目标和语义

本机hentailoliteam.axolotl（Workshop3292351432）最新选1.6 DLL；旧类Axolotl.Verb_LotiIntensifyShoot在实际1.5/1.6都不存在，但明确等价后继 `Verb_LotlQiIntensifyShoot` 被多个现行Weapon XML的verbClass引用，仍是实际射击verb。本轮最小改为此真实类的private CanUse getter，原逻辑不变：GetPawn==null时结果false且跳过原getter；有持有Pawn时保留原getter，不能禁用真正螈气武器功能。GetPawn源码为 `(EquipmentSource?.ParentHolder as Pawn_EquipmentTracker)?.pawn`，地面和inventory仍没有equip owner；1.7 loose Thing.ParentHolder=Map，as tracker会返回null，语义匹配。

**上游已经合入兼容修复**：实际CanUse自身检查GetPawn/PawnComp非null，Projectile/TryCastShot已catch异常，源码注释明确感谢本补丁作者。新guard为保留原意且避免陈旧missingtype，只保留很小冗余null保护，不夸称首次修复上游。没有删除上游功能、重写CanUse或强迫公共化。全部静态API引用api-references.json；实际类型清单、正确类型反编译、源码与live XML都已核对，失败的旧类查找日志清楚留存。

当前已安装SimpleSidearms v1.6实际StatCalculator.RangedDPSAverage / RangedDPS使用 `verbProps.defaultProjectile`，不直接访问verb.Projectile；GettersFilters仍枚举inventory进行DPS选择。因此不能把旧bug描述当成现上游必定在此直接触发。实际反编译在SimpleSidearms-1.6，调用链解释有依据，无桩。递归 HAR/全上游加载仍需root先baseline，旧supportedVersion只到1.6本身不判不兼容。

## 验证及待测项

production build.log和Map检查checks-build.log均0warning/error。命令 `bash scripts/dotnet.sh build work/39-MoelotlSimpleSidearmsCompatiblePatch/1.7/Source/mod.csproj -c Release --no-restore`。

Tests/1.7/Mod39Checks使用当前已加载WeaponDef和真正CompEquippable/Verb，依次真实地图、Pawn.inventory.innerContainer、Pawn.equipment持有链，检查实际GetPawn/CanUse/Projectile fallback；有equipment owner时自有Prefix必须true让原逻辑执行。若真正SimpleSidearms已启用，还在实际inventory态调用其RangedDPSAverage并要求finite正DPS；未启用时明确输出组合调用待测，不能算组合pass。清理generatedobjects。不是用伪Verb或直接Prefix代替现实owner链。场景未执行；无独立算法不造镜像unit。

递归required runtime IDs brrainz.harmony、erdelf.humanoidalienraces（839005762）、hentailoliteam.axolotl（3292351432），组合测试加petetimessix.simplesidearms（927155256）；实际paths在Workshop对应目录。原产品metadata未把可选SimpleSidearms强迫成依赖。待验证：真正MoeLotlPawn螈气开启/关闭的positive能量/弹丸路径、实际SimpleSidearms切换job/图标/选择UI、射击/存档/跨种族、HAR1.7加载与渲染。
