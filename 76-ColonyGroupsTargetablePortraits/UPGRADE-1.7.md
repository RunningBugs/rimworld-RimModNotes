# Colony Groups Targetable Portraits — 1.7

状态：适配/编译完成，实际接口和业务driver待root串行执行。packageId `RunningBugs.ColonyGroupsTargetablePortraits`，无发布ID，按用户追加完整自用MOD范围升级。标题及双语描述包含1.7，旧1.6源码/DLL/依赖副本保持。

新1.7 net10.0工程固定Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2；生产源码只改明确PatchAll(typeof(ModEntry).Assembly)，保留owner/Prefix原逻辑。新工程原有PublicizeAll/包/CS0436抑制已去除：实际TacticUtils.TacticalColonistBar、TacticalColonistBar.TryGetEntryAt/TryGetGroupPawnAt、Entry.pawn/map/group均是public，0/0编译不需扩大可见性。保持必要旧上游TFM reference mismatch兼容设置，但不用旧Source副本或桩。TacticalGroupsAssembly属性默认实际Workshop2345493945/1.6/Assemblies/TacticalGroups.dll，可显式覆盖真实路径；Private=false，不分发依赖。

真实游戏1.7.4977 rev1248的ColonistBar.TryGetEntryAt(Vector2,out Entry)签名和构造Entry(Pawn,Map,int)仍相同；Targeter.CurrentTargetUnderMouse仍用此入口，然后按targetingParameters.CanTarget判合法、处理死亡pawn.Corpse，后续技能valid/highlight/sound/Shift交由原版，不另造能力逻辑。ready TG main hit先于popup，no-hit直接false；只有null bar放行native hit-test，原行为保持。

复用root `expansion/logs/76-tactical-target.cs`，另读实际TacticUtils/世界组件/ColonistGroup/PawnGroup：主栏TryGetEntryAt使用真实Entries、DrawLocs坐标和Size；popup只检查active或right-click的group.pawnRects，并以scrollPosition.y修正y。字段、方法和实际调用均存在，Entry.group/map保留；popup.map来自pawn.Map、group0。安装上游仅声明到1.6，**没有声称存在1.7官方TG版本**；真实运行接口证据与全上游支持分开。哈希/路径见工作区`expansion/logs/76-ColonyGroupsTargetablePortraits/static-validation.json`，完整旧/新静态API audit在同目录。

没有自有XML Def/Patch或Scribe字段；About依赖/加载顺序保持Harmony与TG。默认精确1.7目录优先，最终Assemblies只有自有CGTargetablePortraits.dll，没有deps/Harmony/游戏/TG/publicized DLL。build.log为0warnings/0errors；命令是` scripts/dotnet.sh restore expansion/work/76-ColonyGroupsTargetablePortraits/1.7/Source/mod.csproj --configfile .nuget/nuget.config`及` scripts/dotnet.sh build expansion/work/76-ColonyGroupsTargetablePortraits/1.7/Source/mod.csproj --no-restore -c Release`。

`Tests/1.7/Mod76Checks.cs`（RW17.Validation.Mod76Checks）独立编译0/0，**尚未实际运行**。只引用游戏/Harmony，反射构造/使用真正TG类、typed Entry及真实DrawLocs/Size、真实PawnGroup/世界组件/pawnRects，绝无fake TG实现。调用原生ColonistBar.TryGetEntryAt，测试主栏pawn/map/group、off-map null map、ready no-hit压制不相关native命中、active/right-click/scroll popup、重叠主栏优先、inactive/no-hit负控；nullbar则必须真正执行原版并返回原生fixture entry。再具体移除本Prefix验证原native无法看见TG-only位置，finally按owner/排序恢复并验证生效。恢复全部原static bar/group cache，销毁自身Pawn，test DLL不发布。

这些真实对象布局夹具覆盖接口和命中逻辑，尚不等于物理鼠标/实际能力释放/校验高亮/声音/Shift连选、完整TG绘制/分组/存档/第三方组合验证。完整上游1.6在本1.7运行的baseline错误需root另行比较，不能用0/0编译证明全上游兼容。

运行反馈：`testing/cases/expansion-fa71-cg76/SaveData/rw17-map.json`两功能checks确实true，18.55s/进程0；**整case不能判无错通过**，上游TacticalGroups.WorldComponentUpdate有181次非基线NRE，第一条在checks前。源码最强解释为Headless没有GUI派发而实际Event.current为ThreadStatic无默认对象，上游Update直接解引用；方法不读取bar，FinalizeInit已Reset bar/PreInit字典，因此不以bar未就绪作结论。尚待root依赖单独Headless/普通渲染baseline及真实Event getter观测；准确证据在工作区expansion/logs/76-ColonyGroupsTargetablePortraits/upstream-headless-diagnosis.json。未注入Event/吞错误或改上游，71将另单独运行排除TG噪声。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-fa71-cg76-render-b：46.31秒，正常exit0/未超时，相关实际检查通过；无ERROR。

实际TG1.6 DLL前置Headless因Event.current为空NRE；普通窗口上下文false/null对照明确，46.31秒原生两组检查和实际头像栏GPU截图无ERROR。物理能力点击/Shift另未测。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/76-ColonyGroupsTargetablePortraits。回写后 expansion-local-fa-cg-anime-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
