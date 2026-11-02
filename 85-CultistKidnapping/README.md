# 邪教徒绑架 Cultist Kidnapping

RimWorld **1.5 / 1.6 / 1.7**，需要 **Anomaly** 和 **Harmony**。包名：`RunningBugs.CultistKidnapping`。由 GPT-6.1 SOL 和 CODEX 制作，作者 RunningBugs，MIT 许可证沿用仓库根目录。

邪教的折跃绑架仪式成功后，发起人优先搬运受害者离图，其余人使用原版 Escort 护卫。搬运中断后重新选取最近、能够搬运且能到达地图边缘的成员。暂时无法搬运时在受害者附近防守、继续尝试，取消原版按伤亡比例逃跑的行为。受害者被玩家抱走时仍会围绕其当前位置作战；恢复清醒后遵守原版“倒地或昏睡才能搬运”的规则。受害者死亡或离开该地图后，幸存邪教徒撤离。

真正带人出图才算成功，原版负责绑架登记、信件、心情、关系、任务信号和世界小人。默认在成功绑走后随机 **3–5 天**再来一波原版折跃绑架，成功绑走奴隶也会继续。下一波成功可再次触发；失败不会自行续链。每张地图最多等待一波新的连袭，新的成功不会推迟已有的较早计时。原版难度、场景禁用和事件条件仍然有效，暂时不能触发时每游戏小时重试，因此这些条件可能延后实际到达时间。

模组选项提供连袭开关，以及 **1–30 天整数区间**。关闭取消待发生的连袭；调整间隔影响之后的成功绑架。核心的搬运和护卫行为始终生效。目标、搬运者和连袭计时支持存读档。

安装：把本目录复制或链接到游戏 `Mods/`，启用 Anomaly、Harmony 和本模组。可加入现有存档；移除前结束当前的绑架撤离。玩家自己的折跃绑架和其它邪教仪式保持原版行为。

## 构建

需要 .NET 10 SDK。NuGet 包、CLI 缓存均放在本模组 `.build/`；不修改系统依赖。`Source/` 共用一份源码，三版分别构建到自己的 `Assemblies/`，每版独立锁定 `packages.lock.json`。

| 游戏版本 | 运行时 | Krafs.Rimworld.Ref | Lib.Harmony |
| --- | --- | --- | --- |
| 1.5 | .NET Framework 4.7.2 | 1.5.4409 | 2.4.2 |
| 1.6 | .NET Framework 4.7.2 | 1.6.4871 | 2.4.2 |
| 1.7 | .NET 10 | 1.7.4995-unstable | 3.0.0-rc.4 |

三版均使用 Krafs.Publicizer 2.3.2。1.5 不调用该版不存在的 Map.CanEverExit，仍保留实际到地图边缘的寻路检查；其余核心行为相同。

```bash
./build.sh --locked-mode
```

可用 `CK_DOTNET=/path/to/dotnet ./build.sh --locked-mode` 指定 SDK。发布程序集为 `1.5/Assemblies/CultistKidnapping.dll`、`1.6/Assemblies/CultistKidnapping.dll`、`1.7/Assemblies/CultistKidnapping.dll`，不附带游戏或 Harmony DLL。`LoadFolders.xml` 只加载公共 Def/语言内容及匹配游戏版本的程序集，不会载入测试程序集。

## 验证

真实游戏验证会生成独立 apphost、Mods、SaveData、XDG 和临时目录，写入 `.build/testing/`；只使用一次性地图。三个独立 **1.7** 游戏进程分别检查搬运/接替、读档后实际出图、再读档后连袭和奴隶绑架。测试直接执行原版仪式结果，跳过数小时的前置吟唱；通过前移测试时钟检查事件到期，未模拟连续多天的日常经营。**1.5/1.6 完成相应参考程序集编译；1.6 另核对本机保留的原版源码/IL。未在缺少运行入口的 1.5/1.6 游戏中实测。**

```bash
./test.sh native-check
# 中文内容检查可另用一个新用例名：
./test.sh chinese-check --language 'ChineseSimplified (简体中文)'
```

默认游戏路径 `/Data/SteamLibrary/steamapps/common/RimWorld`，Harmony 路径见 `Tests/Integration.csproj`。其它机器可调整这两个本地路径；`run_game.py --game` 可指定游戏入口。每次使用新的用例名。验证摘要见 [Tests/results.json](Tests/results.json)，实现依据见 [docs/source-audit.md](docs/source-audit.md)，当前规格见 [docs/specs/cultist-kidnapping.md](docs/specs/cultist-kidnapping.md)。

## 预览图

[About/Preview.png](About/Preview.png) 是 Python/Pillow 生成的 1024×1024 黑底白字图。重建使用独立环境：

```bash
uv venv .venv
uv pip install --python .venv/bin/python 'pillow==13.0.0'
.venv/bin/python tools/make_preview.py
```

## English

After a successful hostile cultist skip abduction, the invoker carries the victim away under vanilla Escort protection. Interruptions cause the nearest capable cultist with a reachable exit to take over. Temporary inability to carry does not end the pursuit. Vanilla handles actual off-map kidnapping and its consequences.

Successful escapes, including kidnapped colony slaves, can trigger another vanilla skip abduction raid after 3–5 days. Mod settings disable follow-ups or change the integer interval to 1–30 days. Timers and carrying state survive save reloads. Vanilla incident restrictions can delay arrival, with hourly retries. Requires RimWorld 1.5, 1.6 or 1.7, Anomaly and Harmony. Native integration tests cover 1.7; 1.5/1.6 builds were checked against their corresponding reference APIs.

Made with GPT-6.1 SOL and CODEX.
