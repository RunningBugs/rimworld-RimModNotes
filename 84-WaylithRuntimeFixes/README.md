# Waylith Runtime Fixes [1.7]

修正RimWorld 1.7 Waylith的两处已复现缺陷。需要Harmony 1.7，加载在Harmony之后。packageId：`RunningBugs.WaylithRuntimeFixes`。

| 修正 | 实际效果 | 边界 |
| --- | --- | --- |
| 渲染错误后的idle等待 | 等待期间每50ms检查引擎记录的原始错误并转发，避免失效渲染线程留下积压帧时永久等待 | 健康渲染没有超时；不恢复GPU，不吞异常 |
| CPU纹理记账遗漏 | pixel对象结束生命周期时扣除尚未释放的真实余额，避免GC已回收资源而统计仍累加 | 修正计数；不减少实际内存或显存占用 |

目前仅放行已审计的Waylith二进制：`1.7.4977 rev1248`（MVID `543d4239-0c13-4a34-b1aa-d3f23a6d02d9`）及`rev926`（`16b3c822-6803-4b5d-b286-d759dbd57145`）。其它构建记录`Unsupported Waylith MVID`并跳过。即使发行版本仍是1.7，官方更新后也需要重新审计。启动日志会分别显示两项`active`或`skipped`。

这是针对两处缺陷的本地补丁，没有主播现场日志，尚不能确认能解决其“越玩越频繁、重启缓解”的卡顿。渲染修正更符合不自行恢复的错误等待；计数修正没有已知的TPS提升机制。

## 实现注意事项

渲染补丁替换`WaitUntilIdle`的一个等待调用，保留原后置检查及其它Harmony补丁。未知IL形状跳过。为覆盖已经内联到游戏主循环的调用，还在同步事件核心等待入口精确匹配**当前active渲染线程的idle事件、无限且不可取消的等待**，其它事件及有限/可取消等待保持原实现。Stop清空active之后的旧实例若调用者已内联，不在附加保护范围。

纹理补丁用ConditionalWeakTable关联一个finalizable Lease。Lease终结时读取并清空对象的实际记账余额，避免已经内联的释放调用导致余额副本过期。CWT的ephemeron语义允许value引用key而不永久保留对象；只有获选Lease能够扣账。**像素对象可能延迟至终结器和后续GC才回收，无法保证恰好一个周期。** 不强制GC，不Dispose共享底层资源，不改写游戏DLL。

补丁载入前已丢失的历史计数无法修复；此前已存在且以后完全绕过登记入口的对象也可能不被覆盖。原引擎同对象并发记账不在修复范围。维护知识、禁止做法及证据路径见[AGENTS.md](AGENTS.md)。

## 构建与测试

已有.NET 10 SDK时执行`./build.sh`。可以用`RW_FIX_DOTNET=/path/to/dotnet ./build.sh`指定SDK；依赖缓存在本项目`.build/`，不全局安装。版本锁定：Krafs.Rimworld.Ref 1.7.4977-alpha.1、Krafs.Publicizer 2.3.2、Lib.Harmony 3.0.0-rc.2。

产品目录仅`1.7/Assemblies/WaylithRuntimeFixes.dll`，不带游戏或Harmony DLL。不要把Tests程序集放进产品目录。

隔离功能检查使用真实游戏程序集，参考包不能执行：

```bash
./test.sh /path/to/RW/17/Assemblies
DOTNET_TieredCompilation=0 ./test.sh /path/to/RW/17/Assemblies
```

第二条检查完整优化、已预热/内联的调用者。测试会主动GC和注入无GPU渲染错误，仅适合独立测试进程。`Tests/NativeCheck.cs`供隔离WaylithCLR游戏进程验证，不放进玩家环境。

验证结果与修改记录见[UPGRADE-1.7.md](UPGRADE-1.7.md)。
