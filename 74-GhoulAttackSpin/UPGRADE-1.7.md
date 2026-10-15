> 主代理游戏验证：`mod74-rev1248-j`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod74-rev1248-j`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Ghoul Attack Spin：1.7 beta 升级

基线 `1.7.4977 rev1248`，当前真实 `RW/17` 快照及源码。packageId `RunningBugs.GhoulAttackSpin`、Workshop `3759522562` 不变。1.6 源码/产物/KeyBindingDefs 原字节保留；新增 1.7 独立版本，About/README 声明支持。

## 最小适配与 API

仅新项目 TFM→net10.0、固定 Krafs.Rimworld.Ref `1.7.4977-alpha.1` / Lib.Harmony `3.0.0-rc.2`，启动显式 `.PatchAll(typeof(GhoulAttackSpinMod).Assembly)`。没有直接私有成员访问，不需 Publicizer。其余产品方法正文不变。默认只选 1.7+根，KeyBindingDef 随新版本，原语言目录仍加载，没有额外 Harmony/游戏 DLL。

静态 API 清单 `logs/74-GhoulAttackSpin-api-audit.json`，并审查全部 2 份 C#、反射式 Harmony 属性、override、Scribe/XML：

- `Pawn_DrawTracker.Notify_MeleeAttackOn(Thing Target)`、私有实例 `PawnRenderer.GetDrawParms(Vector3,float,Rot4,RotDrawMode,PawnRenderFlags):PawnDrawParms`、`Pawn.GetGizmos():IEnumerable<Gizmo>` 的实际目标保持存在/签名匹配；Harmony 注入私有 `___pawn` 字段名正确。原渲染准备与 portrait 绘制仍调用 GetDrawParms，结果具有实际 pawn/matrix。没有 transpiler。
- 旋转仍由真实 TicksGame、28 tick 生命周期、Mathf.Clamp01→Quaternion.AngleAxis(up)→Matrix4x4.Rotate 右乘实现。当前 UnityStubs 角轴/矩阵已是 System.Numerics 正常实现，并非空桩；review 了具体转换/转置。该渲染修改不调用新 IME API，但兼容不等于屏幕视觉已验证。
- `Game.GetComponent(Type/T)`、GameComponent.Tick/ExposeData、mapPawns.AllPawnsSpawned、Pawn.IsGhoul/IsColonySubhumanPlayerControlled/Drafted/Dead/Downed、Dictionary IDs 的实际生命周期与签名保留。存档字典键 `enabledByPawnId`、设置 `direction`/`enableAutoFrenzy` 不变。
- `Pawn_AbilityTracker.GetAbility(def,includeTemporary:true)` 必要且仍返回 hediff 临时能力；实际 AdrenalHeart XML 授予 GhoulFrenzy，AbilityDef nonInterruptingSelfCast=true、仅自己、1800 cooldown、实际 GiveHediff comp。原 `Ability.QueueCastingJob` 此分支立即 `verb.TryStartCastOn(verb.Caster)`，没有普通排队 Job；CanCast/CooldownRemaining/已有 hediff 的防重逻辑意义不变。
- Command_Ability.Ability/hotKey、Command_Toggle callbacks、KeyBindingDef SelectionMisc/default None、DefDatabase SilentFail、Scribe_Collections(Value,Value)、Listing/Settings API 匹配；缺 Anomaly 时仍 SilentFail 判空，而非新增 DefOf 硬依赖。实际“不启用 Anomaly”组合尚待运行。

## 编译与断言

```bash
scripts/dotnet.sh build work/74-GhoulAttackSpin/1.7/Source/mod.csproj -c Release --configfile .nuget/nuget.config
scripts/dotnet.sh build work/74-GhoulAttackSpin/Tests/1.7/Mod74Checks.csproj -c Release --configfile .nuget/nuget.config
```

产品和检查均 0 warnings / 0 errors（检查首轮局部变量重名编译问题已修，不涉及产品）；最终日志 `logs/74-GhoulAttackSpin-build.log`、`...-native-check-build.log`。DLL SHA256 `8cac948488a027c83f5207bafe1e3b409875035350fd8b9043053727066bed02`。全部 XML 可解析。

没有既有测试；自有逻辑依赖真实 Pawn、Game、clock、render/ability，未添加复刻实现的假单元测试。`Tests/1.7/Mod74Checks.cs` 为主代理提供真实 Map 断言（已编译、未执行），仅编译引用游戏/Harmony，反射读取 MOD：实际三补丁注册、真实 ghoul melee 通知、调用真实 TickManager.DoSingleTick 七/二十八次并核对真实 TicksGame、顺/逆方向及实际 GetDrawParms 矩阵/过期恢复、人类不旋转、真实 AdrenalHeart 临时能力、原 gizmo 实际 toggle/hotkey、全局/未征召 guards、真实 immediate self-cast→Frenzy/cooldown、不重复剂量与 cooldown 拒绝。两次旋转共推进 56 个实际游戏 tick；只用于一次性测试游戏，未替换/回滚全局 tick。finally 恢复设置、移除测试 pawn state 并清理 pawn/target。

## 剩余验证

实际游戏检查待执行；可视攻击动画/缓存与 portrait 场景、按键重绑定/输入、随机方向统计、非人种食尸鬼、死亡/Downed/跨地图切换、字典真实保存重载及无 Anomaly 组合未覆盖。Headless matrix 断言可以验证变换数据，不能代表屏幕旋转观感或输入功能。没有把 no-error/编译视作功能通过。
