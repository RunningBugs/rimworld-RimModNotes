> 主代理游戏验证：`mod82-render-x11-v3`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod82-render-x11-v3`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod82-rev1248-af`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod82-rev1248-af`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod82-rev1248-ae`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod82-rev1248-ae`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod82-rev1248-ad`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod82-rev1248-ad`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

> 主代理游戏验证：`mod82-rev1248-ac`，进程/加载检查 未通过，真实功能断言 未通过或未执行。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod82-rev1248-ac`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# 1.7 升级记录

目标 `1.7.4977 rev1248`。**独立1.7编译、实际纯逻辑测试、现有静态约束及真实资产解析/SPIR-V重写通过；游戏功能/GPU视觉待执行**。packageId/发布ID保留，原 Source、Assemblies、Defs、语言、Textures、AssetBundles、UnityProject及用户先前BlackHoleJobs.xml修改逐字保留。

## 最小改动与依赖

生产1188行C#逐字复制到1.7/Source，无需改接口或算法：net10.0 +精准Krafs.Rimworld.Ref1.7.4977-alpha.1，输出1.7/Assemblies/BlackHole.dll，与根旧版同相对文件名覆盖去重；About添加1.7。没有Harmony/HugsLib使用，不机械增加依赖；没有private直接编译访问，无需Publicizer。根Defs/语言/资源继续加载，不复制出第二Def/Shader。生产0warning/error（build.log），API全静态引用api-references.json，当前game代码schema与alpha1相等，实际行为另查源码。

## 游戏API、XML及存档语义审查

- CompBlackHole初始化/CompTick/PostDestroy/PostExposeData/Gizmo在实际ThingWithComps调用链存在。**1.7 TickInterval增加未废弃legacy Tick**：Source/Verse/Thing.DoTick:720和ThingWithComps.Tick:329仍每normal tick调用CompTick，60hashinterval卡死提醒不需改为CompTickInterval。PostDestroy仍收到previousMap，原清理Lord路径不依赖已为空的parent.Map。
- TransferableOneWay计数/Thing列表、Matching/MatchingDesperate、TransferAsOneMode.PodsOrCaravanPacking、AddToLoad不跨品质条目合并、持有/已在搬运initialCount扣除、GenClosest lookInHaulSources/Pawn Reserve/CanReserveAndReach/NormalMaxDanger、carryTracker真实SplitStack、Toils_Goto/Haul/JobDriver和ThinkNode_JobGiver override参数均存在。当前spawned Thing.holdingOwner=map，真正carry helper会正常despawn/remove map owner；末Toil从carryOwner.Remove后Devour，无假容器。
- LordJob/StateGraph/LordToil/PawnDuty、强制进Lord/Cancel移除Lord和UnloadEverything、当已有hauler时Enter等待规则与现实际transport loading语义一致。旧Scribe键 leftToLoad、loadingInProgress、notifiedCantLoadMore、initialCount、parent 不变；取消/毁坏/重载仍须实际状态验证。
- Dialog_DevourBlackHole 的实际TransferableOneWayWidget、TabRecord/TabDrawer、AllSendablePawns/AllReachableColonyItems、可达性PassDoors、接受前空选/不可达拒绝、assign beforeStartLoading、WriteUI/声音/翻译API均存在。用户原BlackHoleJobs.xml的emergency=true、Hauling、Manipulation、canBeDoneByMechs、dropThingBeforeJob=false/no opportunistic prefix原样保留；两个JobDef/DutyDef/WorkGiverDef及DefOf/Graphic/Comp类、英语/简中注入键检查通过。
- Graphic_Collection/subGraphics、GraphicRequest/DrawWorker/Material/Mesh/Texture/Samplers/MaterialPropertyBlock/Time.realtimeSinceStartup、Rot4/offset等实际接口及真实引擎实现存在，不用普通SDKTime=0单元结果冒充动画测试。

## Unity资产在当前Waylith中的真实审查

不是只测Shader.Find或isSupported：实际 `RW/17/UnityStubs/UnityStubs/Internal/AssetBundleReader.cs:1448`读取UnityShaderAsset（Vulkan variant），建立AssetBundleShaders/BindingPlan并重写SPIR-V。Unsupported Shader会标记_unsupported/isSupported=false，原Graphic_BlackHole已有Graphic_Twinkle回退；该回退必须另标视觉变化，不能当原自定义效果成功。

用当前**真实程序集快照** `/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies/UnityStubs.dll` 与同目录真实AssetsTools.NET等依赖的CPU检查工具 Tests/BundleInspect（工作区隔离SDK），直接调用当前内部UnityShaderAsset.Read、UnityShaderBindingPlan.Build、SpirvModule.Rewrite，不使用参考空方法体、不启动引擎或修改原资源。证据 bundle-inspect.json/log：

- blackhole_linux：Vulkan index1；原vertex/fragment SPIR-V2592/23128字节，当前实际重写为2952/23192；uniform208字节<=512，storage0<=8，真实CBuffer有_RealTimeSecs；解析/重写无failure。
- blackhole_win：Vulkan index2，同字节/限制/时间字段成功。
- blackhole_mac：只有Metal无Vulkan，当前raw reader返回no Vulkan variant；实际AssetBundleReader.ReadLinuxSiblingShader会在platform缺失时找linux同胞包，根保留linux包，可由目前源码推断fallback路径，但没有Mac实机验证。不能把Linux结果宣称全部平台通过。

当前Linux原bundle已具备实际可导入的Vulkan程序，**不必重烘焙、不改Shader或三个原bundle**。资产大小/hash见asset-hashes.json；UnityEditor BuildBlackHoleBundle源码本就明确Linux OpenGLCore+Vulkan、Windows D3D11+Vulkan+OpenGL、MacMetal。初CPU工具尝试构造完整engine shader，普通SDK缺游戏ShaderRegistry资源路径，失败找不到SceneDefault.frag.glsl（initial-limit.txt）；最终检查有意停在纯解析/重写，不能作为GPU构造/渲染证明。

CPU复现：`TMPDIR=<本MOD Tests/BundleInspect/tmp> bash scripts/dotnet.sh run --project work/82-BlackHole/Tests/BundleInspect/BundleInspect.csproj -c Release --no-restore -- work/82-BlackHole/AssetBundles/blackhole_linux work/82-BlackHole/AssetBundles/blackhole_win work/82-BlackHole/AssetBundles/blackhole_mac`。无新增包或全局环境修改。

## 已执行测试

`bash scripts/dotnet.sh run --project work/82-BlackHole/Tests/Unit/Unit.csproj -c Release --no-restore` passed（unit-tests.log）：实际生产CompBlackHole独立queue管理同def不同transferable不合并、各数量/成员/先后顺序/零数和无Thing条目/非正request边界；实际FrameIndex15个extreme IDs与不同frame counts的overflow/mod范围病例。普通SDKTime为0，仅证明范围，**不证明wallclock animation**。Native game Def/AI/Lord/Destroy等行为不用伪对象作可用证明。

现有43静态XML/源码合同测试复制指向新1.7源码后全部通过（static-tests.log）；它们包含旧作者的字符串约束，所以仅列为静态保护，不冒称43个游戏功能测试。生产与Tests/1.7/checks编译0warning/error。

## root实际功能/GPU场景

Tests/1.7/Mod82Checks只引用game/Harmony，实际自有程序集反射，非伪game类型。真实建筑+有效生成Pawn+真实Silver10：item-only不拉Lord、forbidden负控制、真实WorkGiver查找发绑定targetA/B/count6 job、注册真实haul driver、真正carryTracker split6，执行该已注册job的真实末deliveryToil→实际Devour，要求carryOwner清空/吞掉6而原stack4保留/队列与loading完成。为同步可重复夹具直接定位Pawn到交付点，**未测试完整走路路径**，不把手动末Toil当全自动haul全程通过。

真实selectedCargoPawn+实际StartLoading Lord/duty→CancelLoad要求对象不毁、Lord/queue清理、UnloadEverything；然后实际JobGiver_EnterBlackHole/registered enter driver末Toil吞噬这个**生成测试Pawn**并完成/移除Lord，清理生成objects/世界注册/RNG。产品本来会永久删除选中Pawn，测试仅操作隔离fixture。

root `RW17_BH_RENDER=1` +实际可见X11/Vulkan窗口（专用Xvfb、env -u WAYLAND_DISPLAY、SDL_VIDEODRIVER=x11）时保留真实黑洞、对实际Graphic实例要求shaderMode=true/isSupported且shader名BlackHole/GalaxyTwinkle，明确不能是fallback；镜头移到建筑。独立test MapComponent等待真实PlayerHasControl并用实际TickManager.Pause，不伪注入Time。1秒/2.5秒请求当前真实渲染截图SaveData/blackhole-a.png、blackhole-b.png，记录同game tick而实际DrawWorker写入的_RealTimeSecs增加、实际shader/elapsed到rw17-blackhole-render.json；两PNG真正存在后生成rw17-finish-test供root --wait-for-test退出。root仍必须亲自检查实际像素和可区别时间变化；shader状态/绘制uniform改变不是动画图像成功的替代。

本代理未启动游戏。待真实场景、完整走路/预订竞争/取消在途/障碍/库存来源、多品质同defDevour计数、紧急work priority/机械体/动物、真正load dialog按钮与UI、卡死提醒仅一次、黑洞毁坏以及完整保存重载；跨平台shader和模组组合另测。产品依赖只有游戏；测试驱动需要brrainz.harmony，但不因此向产品metadata加Harmony依赖。

## 取消夹具的实际合同修正

mod82-rev1248-ac在真实carry/split/delivery/devour完成后，cancel复合断言失败。真实Pawn_InventoryTracker.UnloadEverything:31–51不是裸flag：getter需要HasAnyUnloadableThing，setter=true而无可卸物品会立即false。本fixture没放可卸货却要求true，与当前game合同不符，不能据此认定产品失败。新场景把每个值写入console，空inventory真实setter/getter负控制后放实际Silver；只观察真实cancel设置true的setter次数（条件为colonist&&IsPlayerHome），保留Pawn/所有货物不destroy、实际Lord/queue/loading清理，且Silver仍在inventory时必须UnloadEverything真，若已卸必须真实spawn地图安全保留。finally具体Unpatch与清理，不伪造flag/跳过取消逻辑。生产1188行仍无改；新检查待root重跑给出运行值，源码证据与实测结论分开。

## 图形模式取消夹具距离门禁

headless mod82-rev1248-ad通过全部真实装载/取消/吞Pawn断言，生产已应用。图形mod82-render-x11-v2在取消前实际cargo Lord/duty不存在；原radius4随机closewalk可选择黑洞本格/Touch邻格，StartLoading实际EndCurrentJob立即触发自主enter链，提前吞掉是产品合法行为，不能据此前置场景认定图形影响产品。新fixture对hauler、cancel cargo及后续positive enter-order重新定位使用实际可达、Standable、无Edifice、距离平方>=9（至少3格）真实地图格；前后输出cargo毁坏/位置/距离/Lord/duty/loading/map等全部值，取消完整正阴断言与真正enter吞噬不跳过。生产不变。根因距离假设待新真实状态日志/运行证明；未拿失败功能case宣称GPU/截图通过。

mod82-rev1248-ae新选格函数在spawn前用Pawn TraverseParms，被真实Reachability.cs:111的pawn.Spawned前置拒绝，全部候选false；这是新增fixture调用时机错误，无生产失败证据。已改为选格阶段真实map PassDoors路径检查，pawn实际spawn后再单独要求真实CanReach，仍保证距离>=3/Standable/无Edifice与全部cancel/enter控制不变。新checks已重新编译，待下一case。

真实GPU/视觉验证：mod82-render-x11-v3，专用Xvfb/X11/lavapipe，exit0、无新增ERROR。已亲自查看blackhole-a/b.png，黑洞漩涡及星点确实改变；Shader=BlackHole/GalaxyTwinkle（非fallback），游戏tick保持3，真实DrawWorker时间28.78198→30.97219。两帧全图3784像素变化；独立图像内容检查与材质/时间证据相互补充，不以isSupported单独判定。真实carry/取消/吞噬断言同时通过。完整寻路行走/交互弹窗仍未覆盖。

图像阴性控制：洞中心100×100区域(x590,y310)同样3784像素改变，与全图相等，区域外0像素变化；不是时钟/UI/相机位移造成的整图差异。
