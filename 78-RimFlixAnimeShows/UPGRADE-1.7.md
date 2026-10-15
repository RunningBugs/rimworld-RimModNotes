# RimFlix电视节目包1.7内容适配

2026-10-15，实际RimWorld1.7.4977 rev1248。包ID RunningBugs.RimFlix.AnimeShows无发布ID的完整自用项目，用户新范围明确授权。**静态内容/真实接口审查通过；原生上游及功能测试待root执行，绘制未验证**。

## 最小内容升级

只改About增加1.7/name，并将过时placeholder blob/starry示例描述改为实际内容：hypnosis片段，显像管157×128、平板620×256、巨屏902×256各60PNG帧，帧间隔0.25秒。保留1.6标签、原真实节目Defnames/路径/时序/图片/制作脚本/本地原素材，没新增图片、下载或运行ffmpeg；全部旧文件逐SHA保持。标题RimFlix 动漫电视秀 [1.6/1.7]。

本MOD没有C# DLL，纯RimFlix.ShowDef XML和180帧资源；不添加虚假生产编译/NuGet/游戏/HarmonyDLL。测试有独立net10准确Ref/Harmony工程，只用于真实游戏driver，不发布testDLL/obj/bin/deps。项目AGENTS.md的本地自用及版权边界保留，不上传工坊。

## 实际上游与全部API/内容

已读取真正安装的zal.rimflix Workshop3194639480 About/LoadFolders及v1.6/Assemblies/RimFlix.dll反编译ShowDef、CompProperties_Screen、CompScreen、RimFlixSettings/Mod、UserContent，日志UpstreamSource保留全部范围来源/文件。此上游modVersion1.0.7只声明到1.6；不能仅据旧标签blocked，也不能仅依据“DLL在”说1.7可用。root须执行它的独立基线；没改Workshop/上游DLL。

LoadFolders在1.7回退最高v1.6：根+v1.6；VFESpacer启用时会另选Compat/Ultra不同DLL，当前独立测试故意没有该组合。真实上游SHA与路径在expansion/results/78-RimFlixAnimeShows.json，不用假桩引用。

- ShowDef继承Def，secondsBetweenFrames float、sound SoundDef、frames List&lt;GraphicData&gt;、televisionDefs List&lt;ThingDef&gt;、disabled/deleted bool与原XML完全相符。三个defName hypnosis_tube/flat/mega各绑定真实Tube/Flatscreen/MegascreenTelevision；空sound为null合法。
- 每个frames/li实际是texPath+Graphic_Single；全部180实际PNG签名/尺寸/路径/SHA校验，content-audit.json保存逐帧数据。帧数不是资产目录统计误判，真实XML指向180paths，无缺失。
- UserContent真正启动构造遍历用户Shows/禁用设置并更新show/screen时间戳；CompScreen.Shows从实际DefDatabase筛当前TV及非disabled/deleted；ChangeShow(ShowDef)重设show/frame；实际TV.DoTick→CompTick→IsPlaying→RunShow按严格&gt;SecondsToTicks帧间隔前进。
- IsPlaying要求TV朝South、PlayAlways或SleepTimer、非空Show.frames、真实PowerOn；不是只强行把frameIndex设1。PostDraw读取FrameGraphic→GraphicData.Graphic→GetCopy(GetSize)→Graphic.Draw；GetSize实际按TV设定Scale及父Graphic.drawSize/PNG比例、Stretch/Fit/Fill计算。解码/创建Graphic与屏幕呈现分别记录。
- 保存由上游RimFlix_ShowDefName/FrameIndex/ShowTicks/FrameTicks/AllowPawn，不新增自己的存档字段。本MOD不改上游查看job/菜单/设置/API。原用户制作脚本同样保留完整画面等比/黑边规则。

## 已做检查与真实driver

XML解析与逐180帧结构检查通过；独立Tests/1.7/checks.csproj编译0warning/0error。`RW17.Validation.Mod78Checks.Run(Map)`（真实PackageId、仅游戏/Harmony引用，上游经实际程序集反射）检查：3真实ShowDef.modContentPack所属/60帧/timing/targetTV；主线程逐180texture解码尺寸和实际非透明非uniform pixels、非BaseContent.BadTex；创建3实际TV+CompScreen.Shows过滤；真实ChangeShow、powered South播放进入、TV.DoTick连续16ticks令index0→1，再断电和North朝向不推进；实际FrameGraphic读回正确texture尺寸。只更改临时TV/上游PlayAlways并finally恢复。

输出rw17-mod78-content.json明确rendered=false，不伪造可见像素成功。Root串行执行自身必要上游基线后才测本包。真正GUI屏幕摆放/缩放/动画、观看Pawn job、玩家next/select菜单、VFESpacer组合、whole-rws上游状态保存回读仍未测；资产能读/编译没有代替它们。

四文件manifest为About、UPGRADE、两个test源/工程；没有重拷贝/重编码媒体、破坏原assets_src或创建空1.7目录改变默认内容加载。全部精确依赖路径、旧字节保持、API/内容证据见expansion/logs/78-RimFlixAnimeShows/和results。没有由本代理launch、真实回写或commit/push。

实际运行更新：上游Headless case expansion-rimflix-deps SIGSEGV(-11)/18.89s，首native栈CLR/JIT token解析+MonoMod桥，未锁具体RimFlix方法；Headless78-b own检查true但QuitAfterBoot超时180，均不能算整个case通过。普通隔离X11 upstream baseline38.99s正常；expansion-anime78-render44.30s正常exit、无超时、mapchecks true且0ERROR，已经覆盖真实3TV/180纹理/实际frame tick/power及rotation负控制。普通地图截图不等于TV屏幕像素证据。

可选仅测试RW17_ANIME78_RENDER=1：本轮业务检查通过后保留3实际TV，位于摄像机附近同anchor，恢复South/PowerOn、PlayAlways，暂停game以稳定真实frameGraphic并跳摄像机；输出rw17-mod78-render-ready.json。Root用既有GPUcapture取得正常PostDraw画面，未生成假TV图或改产品。仅用于可丢弃隔离case；失败仍finally完整清理，成功保留待该case退出。没有声明此暂停场景证明真实动画速度；Root观察像素结果尚待。

准确case索引：Headless有own断言后超时为expansion-anime78-17-b（180.26s/timed_out=true，不能据returncode0通过）；普通上游为expansion-rimflix-deps-render。另实际独立无harness上游expansion-rimflix-isolated-no-harness11.08s正常exit，说明旧SIGSEGV受测试组合影响，精确根因未锁；不外推上游自身所有模式不能运行。JSON有每轮真实result路径/参数与退出/超时。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-anime78-render：44.3秒，正常exit0/未超时，相关实际检查通过；无ERROR。
- expansion-anime78-tv-render：48.5秒，正常exit0/未超时，相关实际检查通过；无ERROR。

三种电视/180帧真实纹理与实际播放tick及负控通过；48.50秒实际GPU图已查看三TV真实PostDraw。暂停画面不证明动画速度，Headless崩溃/退出超时仍为环境组合边界。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/78-RimFlixAnimeShows。回写后 expansion-local-fa-cg-anime-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
