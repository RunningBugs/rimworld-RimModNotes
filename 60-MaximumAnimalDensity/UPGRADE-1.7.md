> 主代理游戏验证：`mod60-rev1248-c`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod60-rev1248-c`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod60-functional`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod60-functional`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# MaximumAnimalDensity：1.7 beta 升级

packageId：`RunningBugs.MaximumAnimalDensity`；Workshop ID：`3523110341`；游戏基线：`1.7.4977 rev926`。仅修改本工作区副本，旧版文件保留。

状态：**已适配，API/静态/编译通过；真实游戏、设置 UI 与设置保存重载未验证。** 不以编译或无启动错误作为功能通过证据。

## 最小变更

新增 `1.7/Source` 和同名 `1.7/Assemblies/MaximumAnimalDensity.dll`；目标 `net10.0`，固定 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Lib.Harmony 3.0.0-rc.2`。只在新版本将启动调用改为 `PatchAll(typeof(ApplyPatches).Assembly)`，避免 Harmony 无参调用的已知调用栈/内联歧义。原 owner、密度 Postfix、设置值与存档 key 不变。无需 Publicizer。

About 增加 1.7、保留 1.6；README 指向本记录。没有有效 LoadFolders.xml，默认加载独立 1.7、Common、根目录；`.bak` 不参与加载。最终 Assemblies 仅自身 DLL 和 deps.json，没有 Harmony、游戏或 publicized DLL。Tests 位于加载树外。

## API 与语义

完整静态成员/类型元数据引用及原始签名保存于工作区 `logs/60-MaximumAnimalDensity-api.json`；生产代码全部 68 行已检查，没有字符串反射查找、transpiler 或第三方 MOD API。

| API 组 | 真实 1.7 证据与判断 |
| --- | --- |
| `Tile.AnimalDensity` getter / Harmony Getter Postfix | `RW/17/Source/RimWorld/Planet/Tile.cs:159`，仍为 public float getter；基于 biome.animalDensity × 各 mutator.animalDensityFactor；旧 1.6 实现相同，Tile 全文仅 ToString 的反编译呈现不同 |
| 下游调用 | `Source/RimWorld/WildAnimalSpawner.cs:30` 从 map.TileInfo.AnimalDensity 开始，再乘季节、游戏状态及污染因子；目标仍参与实际野生动物数量/生成逻辑 |
| `Mathf.Min(float,float)` | 实际 UnityStubs 类型反编译返回 `MathF.Min(a,b)`，并非未实现桩；新引用的静态元数据直接指向 UnityStubs |
| Startup / Harmony / `Type.Assembly` / Log | 与共享基线一致，显式 PatchAll 自有程序集；`Color.green`、Colorize(string, Color) 和 Log.Message(string) 编译接口存在 |
| `Mod(ModContentPack)` / `GetSettings<Settings>` | `Source/Verse/Mod.cs:13/18`，约束与设置读取流程保留，初始化仍来自实际 MOD 构造函数 |
| `ModSettings.ExposeData` / `Scribe_Values.Look<T>` | `Source/Verse/ModSettings.cs:7`、`Scribe_Values.cs:7`，float/bool 泛型接口、默认 1f/true 与标签 maxAllowedAnimalsDensity/isEnabled 不变 |
| SettingsCategory / DoSettingsWindowContents(Rect) | `Mod.cs:42/46` 仍为对应 public virtual 方法；新版本保留原 override 可见性 |
| Listing_Standard 构造、Begin/End、CheckboxLabeled、SliderLabeled | `Listing_Standard.cs:72/78/215/379`；界面仍是 enabled checkbox 与 0..2 slider，不能用 Headless 验证其视觉/交互 |
| Translate、TaggedString 隐式转换、NamedArgument 与 Single.ToString(F2) | 真实静态引用见 API JSON；所用键存在于 Common 中英 Keyed XML，未改变格式或地区行为 |

Settings 是 MOD 配置，不是新增游戏存档字段。本次保持 XML 标签和默认值；没有迁移数据。Common 的 Def/Patch 是空模板，Keyed 翻译是实际内容，全部 XML 解析通过。

此 MOD 限制的是地块生态密度，后续游戏因素仍可能改变 DesiredAnimalDensity；它不是直接按 Pawn 个数裁剪动物，也不删除已存在动物。

## 构建与验证

工作区 `scripts/dotnet.sh` 设置专用 .nuget 缓存及 .dotnet-home。在工作区依次执行：

```bash
scripts/dotnet.sh restore work/60-MaximumAnimalDensity/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/60-MaximumAnimalDensity/1.7/Source/mod.csproj --configuration Release --no-restore
scripts/dotnet.sh restore work/60-MaximumAnimalDensity/Tests/1.7/Mod60Checks.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/60-MaximumAnimalDensity/Tests/1.7/Mod60Checks.csproj --configuration Release --no-restore
```

MOD 与真实检查代码均零警告、零错误；日志：`logs/60-MaximumAnimalDensity-{restore,build,checks-restore,checks-build}.log`。旧 1.6 文件逐字节核验未变，版本声明、固定依赖解析、XML 和产物布局已检查。

主代理在隔离地图准备好后调用 `RW17.Validation.Mod60Checks.Run(map)`；代码只引用游戏/Harmony，按真实程序集反射读取 MOD 的设置，并在 finally 恢复原值。场景为：实际 Getter 补丁已注册；禁用时 biome/mutator 乘积保留；启用且超过上限时钳制；低于上限不增加；零上限；真实 map.TileInfo.Getter；真实 WildAnimalSpawner.DesiredAnimalDensity 下游得到零。

检查源已编译，尚未运行。单独编译引用不代表普通系统 .NET 可执行游戏参考程序集；本次没有实现复刻式纯函数测试。仍待设置窗口可视交互、中英文标签、Scribe 配置写入/重载、野生动物 tick 行为及组合补丁测试。实际入口与检查成功日志均需主代理保留证据后才能提高验证级别。
