# 1.7 新建与验证记录

2026-10-15：用户授权在RimModNotes新建独立运行时补丁。本项目并非将已有1.6 MOD升级，也没有Workshop ID。

## 改动

- 新增1.7/Harmony元数据、加载目录、固定NuGet引用与锁文件。Publicizer只公开5个所需字段；游戏原DLL不变。
- `RenderWaitPatch.cs`：保留健康等待，在等待期间用Volatile读取引擎记录的错误并原样转发。保留原等待后的检查；IL不匹配跳过。
- 因WaitUntilIdle可能已内联到正在执行的主循环，另为核心等待入口增加精确守卫：只处理当前active渲染线程idle的无限、不可取消等待，所有其它情况透传。50ms有限等待不递归替代，不制造健康等待超时。
- `TextureAccountingPatch.cs`：CWT关联finalizable Lease；仅获选Lease armed，终结时原子取走并清空对象实际余额，再扣减总统计。显式释放已将余额清零时不重复扣除。不Dispose共享core、不操作GPU。
- `PatchInstaller.cs`：仅放行两份已审计Waylith MVID，逐项记录active/skipped；未知版本跳过，避免官方修复后的重复扣账。
- 新增本目录AGENTS.md、README、真实DLL/Harmony功能检查与隔离WaylithCLR检查入口。

## 验证结果

构建使用现有SDK10.0.100，Release，零警告/零错误。产品目录只有`WaylithRuntimeFixes.dll`，不包含Harmony、游戏引用、PDB或deps文件；本次成品大小/哈希记录于工作区`engine-fix/verification.json`。

| 环境 | 已验证内容 | 结果 |
| --- | --- | --- |
| stock.NET10.0.0 + rev1248真实DLL | 默认JIT、真实Harmony安装、50项断言 | 通过 |
| stock.NET10.0.0 + rev1248真实DLL | 禁用分层编译，全优化，预热调用者、50项断言 | 通过 |
| stock.NET10.0.0 + rev926真实DLL | 全优化，同一成品DLL，50项断言 | 通过 |
| 游戏自带.NET10.0.10-waylithclr.dev + rev1248 | 全优化、隔离Headless profile、真实MOD构造加载、51项断言、exit0 | 通过 |
| rev1248隔离Headless新地图 | 默认JIT、51项补丁断言、250×250地图初始化、exit0 | 通过 |

测试覆盖健康慢等待、已有错误、实际worker出错并留有积压、原异常保留、Dispose、有限/取消/非目标等待、分支标签和异常区域、owner隔离、重复安装及半对修复、未知MVID判定和IL不兼容跳过。纹理检查读取原始signed总计数，覆盖显式/重复释放、大小变化、活key、多key并发、CWT同key竞争注册、旧对象收养、预热Track/Release和Unity构造/Destroy、key与像素数组最终回收。

两次最终原生进程的ERROR均仅为Headless基线已有的`Resolution too small (0x0)`；无新增ERROR。没有进行低内存/显存压力或主播场景复现，地图为隔离生成的临时地图。

## 调查中排除的实现

1. 只保存一个不持有key的余额副本并hook Set/Track/Release：全优化已内联Release可绕过hook，造成重复扣除。原CurrentCpuBytes getter把负数夹到0，故仅看显示数字的初版测试不充分。最终实现读取真实余额。
2. 只patch WaitUntilIdle：全优化已内联调用者仍会卡住。四个生产调用均位于EngineApplication；主循环已在执行，重写调用者不能替换当前栈帧。最终增加上述事件身份守卫。

失败探针保留在维护工作区`engine-fix/probes/texture-accounting`及`test-*-optimized-before-inline-fix.log`，不要删去知识后再恢复这些旧方案。

## 限制

没有主播现场日志或存档，未证实修复其卡顿。计数修正不减少真实RAM/VRAM，Lease还可能令像素延迟到终结器及后续GC才回收；不承诺固定回收周期。不修复原始GPU故障、原生驱动挂起、旧历史漏账或原引擎同key并发语义。

rev926仅验证真实引擎DLL的stock运行；当前完整游戏为rev1248，没有把rev926标成完整原生游戏测试。隔离测试不使用/修改玩家配置、存档或正式Mods目录；没有发布、commit或push。

备份：`/home/lisanhu/mine/workspace/rimworld/backups/waylith-runtime-fixes/20261014T220328Z/RimModNotes.tar`；已校验SHA-256。原始调查和最终JSON/日志见`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/engine-fix/`及本目录AGENTS.md的来源路径。
