# Facial Animation Statue Snapshot Fix — 1.7

状态：适配与编译完成，真实功能driver待root串行执行。packageId `RunningBugs.FacialAnimationStatueSnapshotFix`，无发布ID，按用户追加完整自用MOD范围升级。名字/双语描述包含1.7，旧1.6源码/DLL不改。

独立1.7工程net10.0，固定Krafs.Rimworld.Ref1.7.4977-alpha.1和Lib.Harmony3.0.0-rc.2；原生产Main.cs逐字复制，**没有生产源码修改**。不需要Publicizer，保留AssemblyName/Harmony owner，无无参PatchAll。默认精确1.7目录优先，最终Assemblies只含自有FacialAnimationStatueSnapshotFix.dll，没有deps/游戏/Harmony/上游DLL。

真实游戏1.7.4977 rev1248的CompStatue.CreateSnapshotOfPawn仍在保存原生body/head/hair/apparel/gene/hediff状态后清additionalSavedPawnDataForMods并调用CreateSnapshotOfPawn_HookForMods。上游真实安装文件为Workshop1635901197/1.6/Assemblies/FacialAnimation.dll，元数据仅声明到1.6；本补丁不把它虚称为上游1.7。已复用root反编译`expansion/logs/71-facial-target.cs`，核实精准目标`FacialAnimation.HarmonyPatches.PrefixCreateSnapshotOfPawn_HookForMods(Pawn,ref Dictionary<string,object>)`仍有且Prefix仍挂在原生Hook。方法以真实IFacialAnimationController.FullName为key，FaceTypeDefName转ExposableString后Dictionary.Add只有一处。

Transpiler的Dictionary.Add(MethodInfo)匹配、Call静态helper的栈参数、labels/exception blocks迁移逻辑仍正确；IL shape不变，无需扩大补丁。SafeAddIgnoreDuplicate的null/blank guard、ContainsKey先值保留、WarningOnce和原Add仍符合接口。动态目标、Exception/Log/Colorize、Hash/反射签名均逐项审查，完整新旧引用见工作区`expansion/logs/71-FacialAnimationStatueSnapshotFix/api-{original,1.7}.json`。精准目标若变或Add数量非一，原ERROR诊断保留，不用吞错掩盖兼容失败。

同一日志目录的build.log和checks-build.log均0warnings/0errors。构建命令为` scripts/dotnet.sh restore expansion/work/71-FacialAnimationStatueSnapshotFix/1.7/Source/mod.csproj --configfile .nuget/nuget.config`，随后` scripts/dotnet.sh build expansion/work/71-FacialAnimationStatueSnapshotFix/1.7/Source/mod.csproj --no-restore -c Release`；引用哈希/单DLL/XML/源码相等在static-validation.json。

新增`Tests/1.7/Mod71Checks.cs`只引用游戏/Harmony，通过反射使用真实FacialAnimation类型/已安装HeadNormal和HeadSquare Def，生成真实Pawn及CompStatue。它实际制造重复HeadControllerComp，走真实私有CreateSnapshotOfPawn→原生Hook→FAprefix→本transpiler，断言真正ExposableString首值、连续快照、预存字典值和不删/不改组件。临时具体unpatch本transpiler后直接调用原上游函数，捕获其原ArgumentException负控，再按owner/排序finally恢复并重走原生快照；不在游戏日志注入预期ERROR。还验证null上游guard、helper空key/nullvalue和有效单Add输入的IL labels/blocks保留。当前**只编译，尚未运行**。

测试用真实组件，不伪造FA类型；没有本补丁自有序列化字段，不虚构Scribe单测。完整贸易库存/派系领袖生成、InitFakePawn/render pixels、快照保存/重载、所有上游版本/第三方组合同样未测。Root此前19对该实际1.6 FA DLL的限定原生运行证据只作环境参考，不能代替本71检查或全上游1.7声明。

运行反馈：`testing/cases/expansion-fa71-cg76/SaveData/rw17-map.json`两功能checks确实true，18.55s/进程0；**整case不能判无错通过**，上游TacticalGroups.WorldComponentUpdate有181次非基线NRE，第一条在checks前。源码最强解释为Headless没有GUI派发而实际Event.current为ThreadStatic无默认对象，上游Update直接解引用；方法不读取bar，FinalizeInit已Reset bar/PreInit字典，因此不以bar未就绪作结论。尚待root依赖单独Headless/普通渲染baseline及真实Event getter观测；准确证据在工作区expansion/logs/76-ColonyGroupsTargetablePortraits/upstream-headless-diagnosis.json。未注入Event/吞错误或改上游，71将另单独运行排除TG噪声。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-fa71-cg76-render-b：46.31秒，正常exit0/未超时，相关实际检查通过；无ERROR。

实际FA1.6 DLL的重复组件→原生雕像snapshot首值保留与原失败负控通过，rendered46.31秒无ERROR。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/71-FacialAnimationStatueSnapshotFix。回写后 expansion-local-fa-cg-anime-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
