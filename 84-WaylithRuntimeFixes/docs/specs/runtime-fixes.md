---
status: current
updated: 2026-10-15
source: changes/runtime-fixes
---
# Waylith Runtime Fixes 0.1.0

独立RimWorld1.7 MOD，packageId `RunningBugs.WaylithRuntimeFixes`，依赖Harmony1.7。当前支持Waylith MVID `543d4239-0c13-4a34-b1aa-d3f23a6d02d9`（rev1248）、`16b3c822-6803-4b5d-b286-d759dbd57145`（rev926），未知构建不安装。

渲染等待：在WaitUntilIdle等待期间每50ms检查真实failure并原样抛出；健康等待没有截止。增加核心ManualResetEventSlim.Wait(int,CancellationToken)前置补丁，仅匹配当前active渲染线程idle的Infinite、不可取消等待，覆盖已内联的调用者。其它等待透传。保留原后置检查，不清队列、不伪造idle、不恢复GPU。IL不兼容时整对跳过；回滚只撤销自身owner。

CPU纹理记账：CWT ephemeron Lease关联TexturePixelData；构造函数登记新对象，原记账入口登记已有对象。仅GetValue实际返回值armed，竞争工厂败者不扣账。Lease终结时原子取走并清空实际_trackedBytes，然后扣总计数，释放其key引用。不依赖内联可能绕过的余额同步副本；不Dispose共享core、不触及GPU。可能延迟像素对象回收至终结器及后续GC，不是RAM/VRAM优化。

net10.0/x64，固定Krafs.Rimworld.Ref1.7.4977-alpha.1、Krafs.Publicizer2.3.2、Lib.Harmony3.0.0-rc.2及锁文件；仅公开五个需要的字段。参考程序集只用于编译，最终目录只有自身DLL；不改原游戏文件，不自动部署或发布。

真实DLL/Harmony检查：stock默认rev1248、全优化rev1248和rev926各50项通过；WaylithCLR全优化启动及默认JIT250×250地图各51项通过。证据在维护工作区engine-fix/，具体命令、已废弃方案与限制见UPGRADE-1.7.md及AGENTS.md。

未确定主播卡顿根因；不修复原生驱动挂起、补丁前丢失的历史计数、旧inactive实例的已内联等待或同key并发记账语义。rev926未做完整旧游戏原生载入，只有真实引擎stock检查。
