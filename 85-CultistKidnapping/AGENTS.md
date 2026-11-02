# Cultist Kidnapping：维护说明与约束

本文适用于 `85-CultistKidnapping/` 及其子目录。使用标准文件名 `AGENTS.md`，供后续开发会话读取。用户当前指令优先；不要修改或重置其它 MOD 的未提交内容。

## 项目标识与事实源

- 名称：邪教徒绑架 Cultist Kidnapping；作者：RunningBugs。
- packageId / Harmony ID：`RunningBugs.CultistKidnapping`；程序集与 namespace：`CultistKidnapping`。
- 支持 RimWorld **1.5 / 1.6 / 1.7**，依赖 Anomaly 和 Harmony，采用 MIT 许可证。
- 已发布条目：[3832488339](https://steamcommunity.com/sharedfiles/filedetails/?id=3832488339)，本地身份文件为 `About/PublishedFileId.txt`。
- 当前行为以代码和 [docs/specs/cultist-kidnapping.md](docs/specs/cultist-kidnapping.md) 为准；源码依据见 [docs/source-audit.md](docs/source-audit.md)，发布流程见 [docs/publishing.md](docs/publishing.md)。
- 新功能或行为变更先在 `docs/changes/<名称>/` 记录 proposal/tasks，验证完成后归档到 `docs/specs/`。简单文档修正不另建功能变更。不要把待实现方案写成当前事实。

## 必须保持的行为

1. 只接管敌对 Horax 邪教的 AI `SkipAbduction` 成功结果，不影响玩家仪式或其它邪教仪式。
2. 使用原版已经选中的受害者；保留其折跃、DarkPsychicShock、仪式记忆和信件，不重新随机选人或重复施加效果。
3. 发起人有能力时优先搬运，其余邪教徒护卫当前搬运者。中断后选择最近且能移动、操作、携带、预约/到达目标并离图的队员接替。
4. 暂时无人可搬运时，在受害者当前位置防守并继续尝试；普通伤亡比例不能触发放弃。受害者清醒时遵守原版搬运限制，死亡或离开本地图后幸存者撤离。
5. 使用原版 `JobDefOf.Kidnap`、`DutyDefOf.Escort`、寻路和离图逻辑。只有实际出图且原版已登记绑架才算成功；不能把折跃、昏迷或抱起当作绑走。
6. 绑架登记、心情、关系、世界小人、任务信号等交给原版执行一次，不手工重复调用 `KidnappedPawnsTracker.Kidnap`。
7. 连袭默认启用，成功绑走后随机 3–5 个整数天再来一波指定 `SkipAbduction` 的原版袭击；奴隶同样计入，后续成功可继续续链。
8. 连袭间隔允许 1–30 天整数区间。每张地图至多等待一波，新成功不推迟较早计时；关闭取消待发生计时，修改间隔影响之后的成功绑架。
9. 保留原版难度、场景禁用和事件资格限制，失败每游戏小时重试，不通过 `forced` 绕过限制来保证到期必出。
10. 受害者、搬运者、成功状态、待接管记录和连袭计时必须支持存读档。修改 Scribe 字段名、类型或默认值时考虑旧存档兼容。

## 源码分工与容易踩错的顺序

| 位置 | 职责及约束 |
| --- | --- |
| `Source/Patches.cs` | 在 AI outcome 中包装唯一的 `SkipUtility.SkipTo`，记录真实返回对象；`Pawn.ExitMap` 前后确认绑架成功。 |
| `Source/KidnappingMapComponent.cs` | 下一 tick 接管同一个 Lord，保存待接管记录、连袭计时，调用原版 Storyteller。 |
| `Source/LordJob_CultistKidnap.cs` | 搬运者选择、护卫切换、持续争夺、原版 Kidnap JobGiver。 |
| `Source/Settings.cs` | 连袭设置、序列化、范围归一化和设置窗口。 |
| `Source/Build.props` | 三版共用构建配置、依赖和精确 Publicizer 目标。 |
| `Defs/`、`Languages/` | 公共 DutyDef 和中英文文本。 |
| `1.5/`、`1.6/`、`1.7/` | 独立项目/锁文件与各自成品 DLL；不要复制三份业务源码。 |
| `Tests/`、`tools/` | 隔离原生测试、预览图生成、上传准备与发布工具，不加载到玩家游戏中。 |

- **先核对原版再改实现。** 先读实际游戏 `Version.txt` 和相应方法源码/IL，必要时重新反编译；记录版本和哈希。不要拿旧快照或记忆代替当前实现。
- 当前审计基线是 **1.7.4995 rev309**，实际游戏目录为 `/Data/SteamLibrary/steamapps/common/RimWorld`。旁边 `RW/17/Source` 是旧的 1.7.4977 快照。审计详情和哈希保存在 `docs/source-audit.md`，升级时重新核对。
- NuGet 参考程序集只有 API，不可当作可运行的游戏或完整方法体；反编译副本只放在 Git 忽略的 `.build/`，不分发原版代码或 DLL。
- 不在 outcome 尚执行时替换 Lord 状态图。在 tick 边界先清理当前 toil，再 `SetJob`、`GotoToil`；`SetJob` 会清空 CurLordToil，不能反过来清理。
- 保留同一 Lord 的任务标签和离图信号，不随意新建队伍丢失任务关系。
- `Pawn.ExitMap` 会先把搬运者移出 Lord，再登记绑架。Prefix 必须先保存指挥引用，Postfix 再检查实际离图与原版 kidnapped 列表；不能事后只靠 `GetLord()` 查成功。
- 原版 `CanFireNow` 早于 worker 的派系解析。连袭参数要明确指定邪教派系、`PsychicRitualSiege` 分组和 `SkipAbduction` 仪式，避免检查无关派系或随机施放其它仪式。
- Harmony 只在初始化阶段查找/改写目标；不要在 tick、选人或护卫循环中增加反射。需要访问非公开 API 时优先精确配置 Krafs.Publicizer，不默认公开整个程序集。
- 当前 transpiler 要求恰好一个 SkipTo 调用；形状不符应明确报错并重新核对，不猜局部变量编号或吞掉补丁失败。

## 跨版本与依赖环境

| 游戏 | TFM | Krafs.Rimworld.Ref | Lib.Harmony |
| --- | --- | --- | --- |
| 1.5 | net472 | 1.5.4409 | 2.4.2 |
| 1.6 | net472 | 1.6.4871 | 2.4.2 |
| 1.7 | net10.0 | 1.7.4995-unstable | 3.0.0-rc.4 |

- 三版均使用 Krafs.Publicizer 2.3.2，每版有独立 `packages.lock.json`。版本和锁文件应一起维护，不用通配版本随意漂移。
- 1.5 定义 `RW15`，仅跳过该版不存在的 `Map.CanEverExit`；实际边缘寻路检查仍保留。不要删掉 1.6/1.7 对不可离图地图的检查。
- 共用代码必须兼容 net472，范围限幅用 `Mathf.Clamp`；不要直接引入旧运行时没有的 `Math.Clamp` 等 API。
- 使用现有 .NET 10 SDK 和项目脚本。NuGet、CLI 缓存在 `.build/`，Python 使用本模组 `.venv/`；禁止系统 pip、`pip --user` 或全局安装项目依赖。
- `LoadFolders.xml` 加载公共目录及对应版本目录；各版 `Assemblies/` 只放 `CultistKidnapping.dll`，不捆绑 Harmony、游戏参考或测试 DLL。

## 构建与验证

在本模组目录运行：

```bash
./build.sh --locked-mode
./test.sh ck-新的用例名
```

- `CK_DOTNET=/path/to/dotnet` 可指定 SDK。涉及行为、接口、依赖或加载路径的修改，检查三版构建并运行相关原生回归；纯文档修改做内容/路径检查即可，不无故重跑游戏。
- `test.sh` 先构建三版，再启动三个独立 1.7 进程，覆盖搬运接替、搬运中读档、实际出图、计时读档、到期袭击和奴隶续链。每次用新用例名。
- 测试只在 `.build/testing/` 的独立 apphost、Mods、SaveData、XDG 和临时目录运行；不改玩家存档、启用列表或游戏 DLL，不干扰正在运行的玩家游戏。
- 核对实际加载的程序集路径和成品哈希；通过的旧结果不能冒充修改后的测试。记录真实输出和未测边界。
- `Tests/results.json` 是已交付基线：1.7.4995 rev309 共 30 项检查、两次真实读档通过；1.5 仅参考 API/编译核对，1.6 另有现存源码/IL 核对，均未运行对应旧版游戏。不得把旧版编译成功写成实机通过。
- 现有测试直接执行原版仪式结果、跳过前置吟唱，并前移时钟检查到期；不能宣称模拟了连续多天经营或所有地形/模组组合。
- 测试夹具需保持确定性：使用固定种子与合法邪教 PawnKind；直接执行 outcome 前模拟原版成功状态 `ritual.succeeded = true`。清理场地时不可对不可摧毁的 SteamGeyser 调用 Destroy。
- 中断后当前搬运者可能已变化，测试不能假设仍是发起人。不要依赖 Headless 下未经初始化的绘制状态来搬动测试 Pawn。

## 文本、预览与出包

- About 描述保持中英文、实际行为和支持版本一致，保留“由 GPT-6.1 SOL 和 CODEX 制作”及对应英文署名、作者和许可证说明。
- 中英文 Keyed 键名和占位符保持一致；不要把构建、反射或上传内部细节塞进玩家操作流程。
- `About/Preview.png` 为 1024×1024 黑底白字，由 `tools/make_preview.py` 生成；使用 `.venv/bin/python` 和环境内 Pillow，不必调用生图模型。
- `_PublisherPlus.xml` 是出包排除规则的唯一配置；路径是忽略大小写的相对路径前缀，**不是 glob**。PublisherPlus 和准备脚本均自动排除该配置文件本身。
- 保留必要运行资源、三版 DLL、业务源码、项目/锁文件、构建脚本和许可证；排除测试、文档、辅助工具、缓存、虚拟环境、obj/bin 和本 `AGENTS.md`。新加开发文件时同步检查出包范围。

## 上传与 Git

```bash
# 仅准备文件并生成哈希清单，不连接 Steam：
.venv/bin/python tools/prepare_upload.py

# 实际创建/更新公开工坊条目：
tools/publish.sh
```

- 上传脚本会产生外部写入；按当前任务和已有有效授权执行。普通文档修改不需要重新上传工坊；用户明确要求的提交、推送或上传应在检查完成后直接执行，不重复索要确认。
- 永久保留 `About/PublishedFileId.txt` 中的 **3832488339**，后续更新同一条目；不得删除 ID 后重建条目或伪造新 ID。
- 上传前检查 `.build/publish/manifest.json` 和实际 content 目录；不能把游戏 DLL、个人信息、测试存档、缓存或反编译源码传上去。
- 发布工具使用游戏自带 Steamworks.NET 和已登录的 Steam，更新前核对账号所有权/AppID；新建成功后立即保存 ID，防止重试制造重复条目。
- 必须等待 `SubmitItemUpdate` 的成功回调并回读服务端详情，确认标题、可见性和内容；返回工坊链接，不能把发出请求当作上传成功。
- Git 提交本模组所需文件、成品、锁文件、测试摘要和文档；不提交 `.build/`、`.venv/`、bin/obj，不带入其它 MOD 的既有修改。按任务授权推送正常分支，不使用强制推送覆盖远端。
