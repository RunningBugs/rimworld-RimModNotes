> 主代理游戏验证：`mod14-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod14-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod14-wave1`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod14-wave1`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# Surgery Never Fail：1.7 beta 升级记录

状态：已适配并编译，真实手术检查场景代码编译通过；尚未游戏运行或UI验证。只修改work完整副本。

游戏1.7.4977 rev926，packageId RunningBugs.SurgeryNeverFail，发布ID3134567885。保留名称/功能/全部旧版支持与素材。

## 最小修改

新增1.7/Source及同名1.7/Assemblies/SurgeryNeverFail.dll，net10.0；锁定Krafs.Rimworld.Ref 1.7.4977-alpha.1与Lib.Harmony 3.0.0-rc.2。启动改为PatchAll(typeof(Start).Assembly)。删1.7未使用HugsLib模板引用：所有源码没有HugsLib符号，About也没有该运行依赖；真正运行仅需brrainz.harmony。

版本声明增加1.7，无显式LoadFolders，默认选择1.7+Common+根；保留旧DLL相对名称，输出仅本MOD DLL。反射定位protected方法不需要Publicizer，本次没有改写游戏程序集。原1.4/1.5/1.6与Common逐字节保留。

## 全部可发现API/动态目标审查

静态元数据见 logs/14-SurgeryNeverFail/api-references.json，覆盖Harmony、UnityStubs、游戏及框架/CallerFilePath等编译器属性。动态Harmony目标另外按真实源码审查，不以元数据可编译代替目标验证。

- 静态构造Start仍由StaticConstructorOnStartup运行，ContentFinder<Texture2D>.Get(SurgeryNeverFail/surgery)、Translate、SoundDefOf.Mouseover_ButtonToggle、自有ToggleIconData.SetupToggleIcon都保持接口。ToggleIconData只是保存传入Type/Texture/tooltip/SoundDef/action/tag，所有getter和默认Flag=true不变；Logger三个方法仍调用Verse.Log对应入口，无其他算法或原生依赖。
- TargetMethod通过AccessTools.Method(typeof(Recipe_Surgery), CheckSurgeryFail)定位唯一protected bool实例方法，准确参数Pawn surgeon、Pawn patient、List<Thing> ingredients、BodyPartRecord part、Bill bill（RW/17/Source/RimWorld/Recipe_Surgery.cs:8）。Prefix只依赖ref bool __result，签名不受原参数名称影响；没有transpiler/IL假设。
- 当前原方法经recipe.surgeryOutcomeEffect.GetOutcome计算结果，failure时应用addsHediffOnFailure并返回true，否则false。Flag启用时设置false并返回false，跳过完整失败路径；关闭时返回true继续原逻辑。保留旧含义，没有只改结果却让原失败伤害照样执行。
- 检查全部静态上游仍真实调用CheckSurgeryFail：Recipe_RemoveHediff、SurgicalInspection、TerminatePregnancy、RemoveBodyPart、ImplantEmbryo、InstallNaturalBodyPart、ImplantXenogerm、AddHediff、RemoveImplant、GhoulInfusion、InstallArtificialBodyPart、InstallImplant。返回失败则中止相应效果/成功则继续，补丁入口未成为死代码。不会保证绕过此方法的第三方手术也不失败。
- 现有RW/16与真实RW/17的Recipe_Surgery.cs逐字节相同；旧目录整体版本尚未独立证实，当前方法签名及效果结论以固定1.7代码为准。
- 第二目标PlaySettings.DoPlaySettingsGlobalControls(WidgetRow,bool)及GlobalControlsUtility调用仍存在；自有AddIcon仍在worldView时返回，使用WidgetRow.ToggleableIcon。Texture null的Unity比较、tooltip/SoundDef检查及Data/Flag getter/setter保持原样。实际图标/tooltip/点击/空资产路径须可视验证，headless不得冒充UI通过。
- Utils.ToggleIconPatcher等通用命名可能与其他MOD同名；测试按SurgeryNeverFail.Start所属Assembly取本MOD类型，并按具体PatchMethod所属程序集核验，不能只用FullName/owner猜测。
- Common模板success_letter字段与当前LetterDef对应，空Patch、双语Keyed、surgery PNG/DDS均保留，全部XML解析通过。没有新增序列化字段或游戏保存数据，本开关仍是进程静态值，不改旧保存习惯。

## 编译与真实可执行断言

生产工程与Tests/1.7/checks.csproj均0warnings/0errors；logs/14-SurgeryNeverFail保留restore/build/checks-restore/checks-build/api-references日志。

```bash
bash scripts/dotnet.sh restore work/14-SurgeryNeverFail/1.7/Source/mod.csproj --configfile .nuget/nuget.config
bash scripts/dotnet.sh build work/14-SurgeryNeverFail/1.7/Source/mod.csproj --no-restore -c Release
bash scripts/dotnet.sh build work/14-SurgeryNeverFail/Tests/1.7/checks.csproj --no-restore -c Release
```

没有独立纯算法/既有单元测试，未机械重写Prefix作为测试。Tests/1.7/Mod14Checks.cs按约定Run(Map)：核验具体手术Prefix与正确Assembly的工具栏Postfix；生成一次性患者，真实Recipe_Surgery子类包装调用protected目标，设置游戏提供的SurgeryOutcome_Failure（chance=1/failure=true）及addsHediffOnFailure为确定失败控制。关闭时断言返回true且实际添加失败hediff；开启时断言返回false且不再执行失败副作用。不是直接调用Prefix或模拟CheckSurgeryFail；finally还原Flag/随机状态并清理患者。已编译，等待主代理运行。

未测：该场景执行、完整Surgery Job/具体worker成功安装路径、真实UI开关、全部DLC/手术/第三方类型组合。上述确定性控制只验证所补丁方法的真实调用和失败副作用，不宣称已覆盖所有手术。

DLL SHA256=11eb7a4e7c519e45dd31534d5c3cf8e86fd227b66be04e446d80a8f1dbb2a6c7；静态成员40、类型43。
