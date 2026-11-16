---
status: current
updated: 2026-11-12
source: changes/workshop-publishing
---
# 远程火力数据 Ranged Firepower Stats

- 名称：远程火力数据 Ranged Firepower Stats；作者：RunningBugs；packageId/Harmony ID：`RunningBugs.RangedFirepowerStats`。命名空间和程序集为 `RangedFirepowerStats`，目录为 `86-RangedFirepowerStats`。
- 支持 RimWorld 1.7，依赖 Harmony；专家情景读取 Ideology 的职位定义。十二项信息属性统一在独立的“远程火力数据 / Ranged Firepower Stats”分类，Def/语言键使用 `RFS_` 前缀。
- 模组只增加本身的数据条目及信息卡、Gizmo、图表入口。生产代码不扫描其他模组或修改它们的统计显示。没有特定第三方模组的开关、兼容性声明或禁用逻辑。
- 专家/专家＋乱开枪及换枪推演使用内部独立 `ProfileStats.PreviewWorker`。不替换共享 StatWorker，不对全局 GetBaseValueFor/GetValueUnfinalized/FinalizeValue 安装补丁，不改变实际职位、特性、装备。真实目标推演只修改局部 ShotReport 值副本。
- 读取武器实例弹丸主伤害、预热倍率、连发数量与间隔，及射手瞄准时间/远程冷却倍率。每秒60 tick，处理正预热和后续连发的最小 tick 以及首段连发1 tick更新重叠；小人完整射击周期对齐当前1–15 tick的工作检查间隔。炮塔采用自己的平均预热、冷却和连发参数。
- 按合法距离及小数端点显示当前/专家/专家＋乱开枪三种情景。天气使用实际过渡倍率、双方屋顶规则和天气射程上限。标准目标为体型1、站立、无遮蔽；真实目标行读取原版射击报告中的掩体、烟雾、体型、姿势和明暗。
- 逐格表支持射手/屋顶/真实目标选择、刷新和TSV复制；独立图表沿用同一快照，支持命中率/期望DPS切换、图例及悬停读数。信息卡的DPS表和原版射击精度表末尾有超链接。原版射手精度图为1–55格的独立精度系数图。
- 数值为护甲前主伤害估算，炮塔额外索敌等待及补弹工作不计入。爆炸、散布及特殊弹丸数据依据具体机制，不伪造精确单目标期望。
- About/Preview.png 为从实际DPS图窗口选出的1024×1024方形截图封面；同目录保存6张1600×1000图库JPG，依次展示信息页、右侧距离表、精度链接、三情景表、DPS曲线和命中率曲线。使用原版截图模式，无开发图标和HUD；演示小人在一次性地图中设置，图表数值未修改。
- 封面PNG无损编码，图库JPG保持原始尺寸，每张文件小于1MB。`tools/make_preview.py` 从验证截图重建素材，`docs/workshop-media.json` 保存来源、尺寸和哈希。
- `tools/prepare_workshop.py` 生成 `dist/RangedFirepowerStats/`，仅含运行程序集、Def、语言、About素材及许可证；保留已有发布ID。游戏 `Mods/86-RangedFirepowerStats` 链接到此目录。源码、开发环境、构建缓存及测试入口不进入上传目录。
- `_PublisherPlus.xml` 是出包筛选的唯一配置，显式排除 dist、开发环境、测试、源码、工具及缓存；dist/ 也被 Git 忽略。准备脚本按实际 PublisherPlus 的忽略大小写路径前缀规则选择同一文件集合，并核对哈希；配置文件本身不进入上传包。
- `tools/publish.sh` 使用本机已登录 Steam 及游戏自带 Steamworks.NET，无需启动游戏界面。支持 --check 只读预检和 --verify 回读核验；正式上传包含中英文描述、封面和六张图库。新建后立即保存ID，更新前核对所有权/AppID；按图库文件名更新，保留已有其他图片。
- 已公开发布条目 **3838056458**，服务端核验了内容、标题、描述、公开状态、六张图库和 Harmony 必需项。ID 在源码及 dist 的 About/PublishedFileId.txt 中持久化，后续更新复用。发布记录见 docs/published.json。
- 旧启用条目的packageId已按新名称替换并保留位置，其余启用列表不变。备份和安装记录见忽略的 `.build/backups/` 及 `docs/installation.json`。无需改写玩家存档。
- 计算来源见 `docs/source-audit.md`，验证见 `Tests/results.json`。检查覆盖真实职位/特性/局部射击报告对照、实例修正、射击周期、表图一致、超链接、分类、中文界面和发布目录加载；另核对现有模组统计仍可见、数值不受预览影响。
