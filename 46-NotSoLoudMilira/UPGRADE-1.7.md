# 1.7 beta 升级审查记录

状态：**未更新／阻塞：上游未安装，17 个强制替换目标无法核验**。基线 `1.7.4977 rev926`；发布 ID `3423639025`；packageId 保留原拼写 `RunningBugs.NoSoLoudMilira`。

未给 About 虚加 1.7，原 1.4/1.5/1.6 声明、Common XML、LoadFolders.xml.bak 和其他原文件全部逐字节保留。此 MOD 无生产 C#、DLL、Harmony 或自有序列化；只对 17 个 Milira SoundDef 的 subSounds/li/volumeRange 进行强制 PatchOperationReplace。默认根目录/Common 加载规则适用。

已完整审查全部 17 个 XPath 与替换值（逐項见工作区 `logs/46-NotSoLoudMilira/static-validation.json`）。真实 1.7 `Verse/SoundDef.cs:57` 仍有 List<SubSoundDef> subSounds；`Verse/Sound/SubSoundDef.cs:35` 仍有 FloatRange volumeRange，`RandomizedVolume:233` 仍返回随机区间值除以 100。因此游戏音量字段的结构/语义已核验，XML 解析通过；这些不证明第三方目标仍存在。

实际依赖 `ancot.milirarace`/Workshop 3256974620 本机不存在。逐个解析本地和 Workshop 的直属 About/packageId 后仍无对应项；简单文本搜索命中 3294268706 是别的手术补丁的 loadAfter 引用，不能当作上游。已查 [作者 Milira 页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3256974620)，所列前置包括 Ancot Library、Humanoid Alien Races 和 Royalty/Biotech；作者公开 GitHub 页面只有图片仓库，没有可用于核验这 17 个 Def 的源文件。未安装、修改或替换上游及前置。

由于现有补丁强制替换，目标重命名、缺失或子声结构变化会造成失败；现阶段没有足够证据把它认定为仅标签升级。按“接口不确定时 blocked”边界保留旧声明，不能通过删除失败补丁、条件跳过目标或空驱动把原功能静默降级。

纯 XML 无生产编译/函数单元测试。测试专用 `Tests/1.7/Mod46Checks.cs` 已独立精准 Ref 1.7.4977-alpha.1 编译，0 warnings/errors（checks-build.log），但未在游戏执行。它检查全部 17 个实际 SoundDef、所有 subSound 的 min/max，并各调用真实 RandomizedVolume 20 次验证计算区间；使用 Rand.PushState/PopState 避免改变后续随机序列，缺目标明确失败。无需把测试编译器或 DLL 加入 MOD 加载目录。

继续所需条件：取得对应、可在 1.7 加载的 Milira 及完整前置，复核上游原始全部 XPath 的唯一目标和子声内容；必要时仅在独立 1.7 Patch 里改实际差异，再添加 1.7 标签。随后主代理做依赖基线、单 MOD+前置加载、Mod46Checks 及按需实际音频试听。Headless 可证明数据/计算，不能证明听感。当前测试方案不等于已完成升级。
