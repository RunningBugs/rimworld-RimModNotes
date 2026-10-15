# Waylith Runtime Fixes：项目知识与约束

## 目标与授权

用户于2026-10-15要求在RimModNotes创建独立补丁MOD，处理此前实际验证的两个问题：渲染错误后的idle无限等待，以及CPU纹理内存统计漏减账。新项目允许没有Workshop ID；这是用户明确新增的本地开发范围，不能套用旧“只升级已发布MOD”的限制。

- 目录：`84-WaylithRuntimeFixes`；程序集/namespace：`WaylithRuntimeFixes`；packageId：`RunningBugs.WaylithRuntimeFixes`。
- 支持RimWorld **1.7**；维护旧项目和用户新增83等内容，不修改其它MOD。
- 不创建假的PublishedFileId，不发布工坊，不自动commit/push。创建前已备份整个RimModNotes，备份入口见本文构建工作区的`engine-fix/backup.json`。
- 本MOD只修复两个明确缺陷；没有授权加入特定persistent instancing descriptor修复、通用内存清理、自动重启或GPU压力策略。

## 已验证知识与结论边界

本地基线为`1.7.4977 rev1248`，固定真实程序集在`/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies`；Waylith.dll SHA-256为`850e99644283a94e78bd6dc5cf0bbfa25367d8e1e11c1acb1f82e9f73a321b37`。旧rev926在`RW/17-rev926`。它们是快照，不使用旧`RW/16`中失效的动态DLL链接。

### 渲染等待

- `Waylith.Rendering.RenderThread.Enqueue`增加pending并重置idle；渲染循环出错只完成当前帧，剩余积压帧可能无人处理。
- `WaitUntilIdle`先等待idle，之后才检查failure；在错误发生与主线程等待交错的条件下，错误已经记录却无法转发。
- 真实未修改Waylith.dll、无GPU、stock.NET10隔离测试10轮复现。正常帧及无积压错误对照能结束；等待期间检查failure的参考对照10轮均能报错退出。
- 原主循环和Enqueue已有错误检查，所以不是所有渲染错误都会卡死。补丁应仅替换该等待点，在等待期间检查已有failure；健康渲染继续按原语义等待。
- 全优化测试已证实WaitUntilIdle可被调用者内联；正在执行的EngineApplication.RunLoop即使重写调用者，也不会替换当前栈帧。附加保护只能在ManualResetEventSlim的核心`Wait(int,CancellationToken)`入口精确匹配**当前active RenderThread的_idle、Infinite且不可取消**时执行故障轮询。其它event、有限等待、可取消等待必须直通；50ms有限调用也直通以避免递归。
- 附加保护范围限当前active实例；Stop清空active后的旧实例若调用者已内联，不承诺覆盖。普通非内联WaitUntilIdle仍由原transpiler保护。不声称能修复没有引擎failure记录的原生驱动挂起。
- **不吞异常、不制造“健康等待超时”错误、不伪造idle、不清空或继续执行失效GPU队列。** 转发真实错误不等于修复原始GPU故障或恢复渲染。
- 这更符合卡住不自行恢复的情形，不能直接声称解决主播的反复临时卡顿。

### 纹理统计

- `Waylith.Rendering.TexturePixelData._trackedBytes`计入静态`_currentCpuBytes`；只有显式ReleaseTracked减账。
- 原类型没有finalizer；UnityStubs.Texture的finalizer也不Dispose共享CoreTexture。仅丢引用时实际对象/像素数组可以GC回收，计数却残留。
- 24个128×128 RGBA纹理探针：显式Destroy后归零；GC-only对象均回收，计数仍有1,572,864字节。
- 当前只见计数用于内存显示，不是显存预算控制。**修正统计不等于减少OS内存提交量、释放更多显存或修复内存泄漏。**
- 实际采用ConditionalWeakTable的ephemeron语义：其value可以引用key而不会永久保留无外部引用的key/value环。Lease终结时读取对象的真实剩余`_trackedBytes`，清空该余额并扣减全局统计；不保存需要与引擎同步的余额副本。
- 先前的“令牌不引用key、每次Set/Track/Release同步余额”方案已废弃：stock.NET全优化、预热调用者中的Release被内联后，hook会漏掉释放，造成双减。不能回退该方案，即使`CurrentCpuBytes`显示0；原getter会把负数夹到0，必须检验原始`_currentCpuBytes`。
- 构造函数登记新key，记账入口登记尚存活的旧key。CWT竞争工厂可能产生未获选的Lease，只有GetValue实际返回的Lease可armed；落选Lease不能扣账。终结读取后清空其key引用。
- 代价：finalizable Lease可让key及像素数组延迟至终结器和后续GC才回收；不能承诺恰好一个周期。这不是RAM优化。测试多轮GC确认最终回收，不要求一次GC立即回收。原同key的SetTrackedBytes并非线程安全，本补丁不扩展其并发语义。
- **不要在Unity wrapper finalizer中强行Dispose CoreTexture**：底层资源可能仍被路径缓存或别的所有者使用；也不要从新增finalizer调用GPU、清空像素数据或强制GC。
- 补丁加载前已经丢失的历史计数不能凭空推算；不把整个全局计数重置为0。

用户观察来自主播，且无法提供版本、日志、回放。用户本机RAM/VRAM明显更高；本机是否卡顿不能作主播故障的反证。测试用于补丁正确性和载入兼容性，不作为主播根因证明。

## 最小实现与升级规则

- 使用Harmony，仅修改内存中的目标方法，不能替换/重写游戏安装的Waylith.dll或UnityStubs.dll。
- 默认对已验证引擎构建启用；版本/方法形状不匹配时明确记录并跳过，不猜字段偏移或继续危险补丁。引擎更新后先复核API和原实现，再扩展允许基线。
- 尤其要防止官方后续加入自己的计数回收机制后双重减账；检查目标类型finalizer和记账路径，未知构建不能静默启用。
- 仅publicize五处确需字段：RenderThread._failure、_idle、_active以及TexturePixelData._trackedBytes、_currentCpuBytes；publicizer只作用于编译引用，不作用于真实游戏文件。
- 注意JIT内联：真实入口调用及已预热入口都要验证，不能只反射直接调用被patch的私有函数就宣称正常调用生效。
- 产品目录只放本MOD DLL；不捆绑游戏引用、Harmony或NuGet依赖DLL。
- 不添加无必要设置页、轮询所有资源的后台线程或定时强制GC。Lease只由CWT关联，不放进另一个强引用全局集合；它对key的引用依赖ephemeron语义，必须验证最终回收。终结器不调用GPU、Dispose、日志或任意外部回调。

## 构建环境

维护工作区：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7`。先读该处`shared/README.md`、`shared/dependencies.md`及`engine-analysis/DEEP-INVESTIGATION.md`，避免重复分析和混淆已证实/未证实结论。

- TFM `net10.0`，x64；当前现有SDK `10.0.100`。依赖固定为`Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Krafs.Publicizer 2.3.2`、`Lib.Harmony 3.0.0-rc.2`。
- NuGet缓存、CLI home、bin/obj使用项目或维护工作区专用目录，不安装系统/用户全局包。
- 参考包只编译，隔离运行使用固定真实程序集。当前本机Harmony1.7与NuGet3.0.0-rc.2字节匹配，更新后重核。
- 工程路径`1.7/Source/WaylithRuntimeFixes.csproj`；输出`1.7/Assemblies/WaylithRuntimeFixes.dll`。命令和实际版本在README及验证记录补充，依赖解析结果锁定。

## 验证要求

1. 编译零错误，检查产品目录仅本MOD程序集；XML/packageId/1.7加载路径有效。
2. 用真实Harmony对真实DLL装补丁：正常等待、慢但健康等待、渲染错误加积压、错误已经存在、退出/释放边界；确认等待能结束而真实异常未被吞掉。
3. 纹理统计覆盖GC-only、显式Destroy、重复ReleaseTracked、改变计数字节、key保持存活、实际数组最终回收、ephemeron环及竞争工厂败者；检查原始计数没有负值或双减。
4. 校验普通入口和预热入口，不仅验证私有方法的直接调用。
5. 在隔离profile做1.7/Harmony/新MOD载入与实际补丁生效检查；不更改用户真实配置、存档、工坊、正在运行的游戏。不用高配置本机性能评价替代低配置主播环境。
6. 记录patch未加载、引擎未知、测试失败时的明确状态，不以无红字或编译成功替代功能验证。

全部修改、验证和未测边界写入README、`UPGRADE-1.7.md`及当前规格。文件分工由root管理，不让代理同时改同一源码/工程或共享文档。

已交付验证：stock.NET10默认与全优化rev1248、全优化rev926各50项断言；游戏自带WaylithCLR全优化载入与默认JIT的250×250隔离地图各51项断言，均通过。当前实现规格在`docs/specs/runtime-fixes.md`；维护工作区`engine-fix/verification.json`记录成品哈希和证据路径。原生测试仅含Headless基线已有的分辨率ERROR。

## 历史调查材料

- 总调查：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/engine-analysis/DEEP-INVESTIGATION.md`。
- 渲染等待复现：`engine-analysis/deep/render-wait/`。
- 生命周期与计数复现：`engine-analysis/deep/lifecycle/`。
- 固定预算策略检查没有自激迁移、持久缓冲64万次版本替换能回收；特定persistent实例分支有回收遗漏但原版未找到触发，不并入本MOD范围。
