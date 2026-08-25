# AGENTS.md — The Sea Expansion 调研笔记库

> 本文件面向 AI 编码/调研 agent，介绍本仓库的性质、结构与工作约定。

## 项目概览

本仓库是 RimWorld Mod「**The Sea Expansion**（海洋扩展）」的**前期源码调研与设计文档库**，不是代码项目：

- **没有任何源代码、构建脚本、测试或依赖清单**（无 pyproject.toml / package.json / .csproj 等），因此也不存在 build / test / lint / deploy 命令。
- 全部内容是 Markdown 文档与 TSV 清单，自然语言为**中文**，所有新增/修改文档必须使用中文。
- 调研对象是**仓库外部**的 RimWorld 1.6.4871 rev598 反编译源码（9213 个 `.cs` 文件），位于 `/home/lisanhu/mine/workspace/rimworld/RW/16/Source/`。该目录在本仓库之外，引用其中的文件与行号时保持绝对路径或 `文件名.cs:行号` 的写法。
- 最终目标：为"海洋地图格"Mod 做技术可行性论证与方案设计（海底出生点、恒温 4°C、永暗、海水/气密、水生生物、排水阀等，原始设想见 `Concepts.md`）。

## 目录结构

```
├── Concepts.md            # 原始设想：10 条用户构思（一切调研的出发点，勿改语义）
├── Concepts.Expanded.md   # 主交付物：扩充稿 v2（穷举调研版），§0~§13 设计结论 + §12 待决策清单
├── COVERAGE.md            # 第二轮调研覆盖报告：9213 文件 100% 处置的统计与分片明细表
├── coverage/              # 覆盖率的底层数据（清单 + 处置记录）
│   ├── manifest.tsv       #   45 个分片的元信息：chunk 名 / 命名空间组 / 文件数
│   ├── skipped.tsv        #   9 个整目录跳过登记（第三方库等）及原因
│   ├── chunks/            #   chunk-NN.txt = 每片文件绝对路径清单；skip-*.txt = 整目录清单
│   └── results/           #   chunk-NN.tsv = 逐文件处置记录（路径 \t 状态 \t 一句话说明）
└── docs/research/         # chunk-01.md ~ chunk-45.md：第二轮逐分片调研发现文档
```

## 调研流水线（数据如何流动）

1. `coverage/chunks/chunk-NN.txt`：按命名空间分组 + 文件名字典序切分的文件清单（45 片，每片约 200~250 个文件）。
2. `coverage/results/chunk-NN.tsv`：对清单中每个文件的处置记录，三列：`绝对路径 \t 状态 \t 一句话说明`。状态含义：
   - **NOTE**：逐文件阅读后有海洋相关发现，发现写入 `docs/research/chunk-NN.md`；
   - **PASS**：实际读过，确认与海洋语境无关；
   - **SKIP**：极 trivial 文件（纯数据类/枚举/接口/DefOf），一句话登记跳过原因。
   整目录跳过的（第三方库等）不入分片，登记在 `coverage/skipped.tsv` + `coverage/chunks/skip-*.txt`。
3. `docs/research/chunk-NN.md`：每片的调研发现文档，开头有 TL;DR，正文按主题分节；关键结论引用 `文件名.cs:行号`，家族级汇总可通过同片 `coverage/results/chunk-NN.tsv` 的逐文件记录回溯；只读到关键段落或结论依赖其它分片的，在文内标注「未验证」。
4. `COVERAGE.md`：汇总统计（NOTE 1192 / PASS 5943 / SKIP 2009 / 整目录 69，合计 9213），声明 results 与 chunks 清单逐行 diff 校验一致。
5. `Concepts.Expanded.md`：基于全部分片发现写成的设计文档，每条结论标注来源分片（如〔chunk-37〕），行号以分片文档为准。

## 文档写作约定

- 语言：**中文**；代码标识符、类名、文件名、Def 名保持英文原文。
- 引用源码的格式：`` `ClassName.cs:行号` ``（正文中）或〔chunk-NN〕（Concepts.Expanded.md 中标注来源分片）。
- 结论与证据分离：`Concepts.Expanded.md` 的设计结论必须回溯到具体分片；分片中的关键机制结论必须回溯到源码行号，家族级处置摘要可回溯到同片 results；禁止无来源的推测，不确定的标「未验证」。
- 第一轮关键词式调研归档已删除，当前事实源只有根目录 `Concepts.Expanded.md`、第二轮分片文档及 coverage 数据；不得引用已删除的 v1 结论。
- 若改动会影响覆盖统计（新增/重读文件、改状态），需同步更新对应 `coverage/results/chunk-NN.tsv`、`docs/research/chunk-NN.md` 和 `COVERAGE.md` 的数字，保持三者一致。

## 原版素材优先的设计边界

本 Mod 当前定位为**原版素材优先的机制型扩展**，而不是另做一套海洋内容包。除非用户以后针对具体内容明确同意，否则调研、设计与实现均遵守以下边界：

- 默认允许新增支撑机制所必需的代码、中文文本和机制型 Def，例如 `BiomeDef`、`TerrainDef`、`GameConditionDef`、`MapGeneratorDef`、`DefModExtension`；新增这些定义不等于获准扩充动物、装备或资源内容。
- 新增的海底 `BiomeDef` 是独立生成规则与参数的载体，应共享生成框架并复用原版内容池；不同海底 biome 不应各自附带一套新素材。
- 地形、建筑、服装、生物、植物、鱼类、石材、矿物、武器、派系、遗迹、特效与声音均优先复用原版及 DLC 已有 Def、贴图、shader、布局和音频。需要视觉差异时，优先使用原版材质的染色、参数或组合，不默认制作新位图。
- 默认不新增动物、植物、鱼类、石头、矿物、武器、服装、派系或玩家可建造建筑；没有合适的原版内容时，宁可让对应生态或生成表为空，也不能为了填充内容自行创造新物种或资源。
- 不修改原版物品与生物的造价、伤害、护甲、产量、市场价值、繁殖率等平衡数值。海底环境本身必需的生成筛选、事件过滤、光照、温度、气密和进水规则不视为改动原版物品数值。
- 若机制确实无法借助原版素材完成，可以提出新增素材或内容型 Def 的方案，但必须先说明必要性、复用方案为何不足以及可能的平衡影响，并取得用户针对该项的明确同意后才能写入正式设计或实现。
- 当前已允许的最小新增范围包括：多个海底 `BiomeDef`、必要的海水 `TerrainDef`、独立海水环境组件及配套环境/地图生成 Def；它们应尽可能复用原版水体 shader、颜色、音效、地形、建筑、装备和数值。

## 当前状态与后续方向

- 第二轮穷举调研已 100% 完成，当前处于**设计定稿前阶段**：`Concepts.Expanded.md` §12 列出 10 项待决策点（方案 A/B 终选、建材气密规则、商队通道、空投落点、海水地形 tag 等）。
- 两个核心架构岔路（详见 `Concepts.Expanded.md` §0）：① 海水环境复用 Odyssey 真空系统（`inVacuum=true`）vs 自建含水性系统；② 世界地图走新 BiomeDef vs 新 PlanetLayer。
- 下一步（尚未开始）：按决策结果创建实际 Mod 项目（C# + XML Defs，RimWorld Mod 标准结构）。本仓库届时大概率作为设计参考保留。

## 给 agent 的注意事项

- 不要在本仓库里寻找或执行构建/测试命令——它们不存在。"验证"一个改动的含义是：检查文档间的交叉引用与统计数字是否一致（可用 `diff` 比对 chunks 清单与 results 行数）。
- 阅读调研结论时优先顺序：`Concepts.Expanded.md`（结论）→ `docs/research/chunk-NN.md`（证据与行号）→ `coverage/results/chunk-NN.tsv`（文件级处置记录）。
- 仓库引用的反编译源码在 `/home/lisanhu/mine/workspace/rimworld/RW/16/Source/`，需要核对行号或深读某个文件时可读该目录；注意它在本仓库之外，只读不写。
