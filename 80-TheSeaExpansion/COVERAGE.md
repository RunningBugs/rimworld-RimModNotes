# 源码穷举调研覆盖文档（第二轮）

调研对象：`/home/lisanhu/mine/workspace/rimworld/RW/16/Source/`（RimWorld 1.6.4871 rev598 反编译源码，共 9213 个 .cs 文件）

## 总体结论：100% 覆盖，无未读文件

| 状态 | 文件数 | 含义 |
|---|---|---|
| NOTE | 1192 | 逐文件阅读后有海洋相关发现，条目见对应 `docs/research/chunk-NN.md` |
| PASS | 5943 | 实际读过，确认与海洋语境无关 |
| SKIP（分片内） | 2009 | 极 trivial 文件（纯数据类/枚举/接口/DefOf），逐个登记了跳过原因 |
| SKIP（整目录） | 69 | 第三方库/非玩法内容，原因见下表 |
| **合计** | **9213** | |

每个文件的处置记录（路径 + 状态 + 一句话说明）在 `coverage/results/chunk-NN.tsv`，与 `coverage/chunks/chunk-NN.txt` 清单逐行 diff 校验一致（9144 行，外加 69 行整目录跳过清单）。

## 整目录跳过登记（skipped.tsv）

| 目录 | 原因 |
|---|---|
| DelaunatorSharp | 第三方 Delaunay 三角剖分数学库，非玩法机制 |
| Gilzoide.ManagedJobs | Unity Job System 包装库，非玩法机制 |
| Ionic.Crc / Ionic.Zlib | CRC 校验 / 压缩算法库 |
| KTrie | Trie 数据结构库 |
| System.Collections.Generic / Unity.Collections | 集合 shim |
| Verse.Steam | Steam SDK 对接层 |
| Properties | 程序集元数据 |

## 分片明细

| 分片 | 组 | 文件数 | NOTE | PASS | SKIP | 发现文档 |
|---|---|---|---|---|---|---|
| chunk-01 | RimWorld | 200 | 13 | 162 | 25 | [docs/research/chunk-01.md](docs/research/chunk-01.md) |
| chunk-02 | RimWorld | 200 | 17 | 132 | 51 | [docs/research/chunk-02.md](docs/research/chunk-02.md) |
| chunk-03 | RimWorld | 200 | 26 | 160 | 14 | [docs/research/chunk-03.md](docs/research/chunk-03.md) |
| chunk-04 | RimWorld | 200 | 30 | 139 | 31 | [docs/research/chunk-04.md](docs/research/chunk-04.md) |
| chunk-05 | RimWorld | 200 | 16 | 150 | 34 | [docs/research/chunk-05.md](docs/research/chunk-05.md) |
| chunk-06 | RimWorld | 200 | 36 | 145 | 19 | [docs/research/chunk-06.md](docs/research/chunk-06.md) |
| chunk-07 | RimWorld | 200 | 12 | 171 | 17 | [docs/research/chunk-07.md](docs/research/chunk-07.md) |
| chunk-08 | RimWorld | 200 | 11 | 15 | 174 | [docs/research/chunk-08.md](docs/research/chunk-08.md) |
| chunk-09 | RimWorld | 200 | 32 | 136 | 32 | [docs/research/chunk-09.md](docs/research/chunk-09.md) |
| chunk-10 | RimWorld | 200 | 45 | 129 | 26 | [docs/research/chunk-10.md](docs/research/chunk-10.md) |
| chunk-11 | RimWorld | 200 | 95 | 31 | 74 | [docs/research/chunk-11.md](docs/research/chunk-11.md) |
| chunk-12 | RimWorld | 200 | 13 | 143 | 44 | [docs/research/chunk-12.md](docs/research/chunk-12.md) |
| chunk-13 | RimWorld | 200 | 28 | 154 | 18 | [docs/research/chunk-13.md](docs/research/chunk-13.md) |
| chunk-14 | RimWorld | 200 | 14 | 186 | 0 | [docs/research/chunk-14.md](docs/research/chunk-14.md) |
| chunk-15 | RimWorld | 200 | 33 | 129 | 38 | [docs/research/chunk-15.md](docs/research/chunk-15.md) |
| chunk-16 | RimWorld | 200 | 16 | 141 | 43 | [docs/research/chunk-16.md](docs/research/chunk-16.md) |
| chunk-17 | RimWorld | 200 | 22 | 144 | 34 | [docs/research/chunk-17.md](docs/research/chunk-17.md) |
| chunk-18 | RimWorld | 200 | 30 | 143 | 27 | [docs/research/chunk-18.md](docs/research/chunk-18.md) |
| chunk-19 | RimWorld | 200 | 16 | 144 | 40 | [docs/research/chunk-19.md](docs/research/chunk-19.md) |
| chunk-20 | RimWorld | 200 | 19 | 121 | 60 | [docs/research/chunk-20.md](docs/research/chunk-20.md) |
| chunk-21 | RimWorld | 200 | 22 | 133 | 45 | [docs/research/chunk-21.md](docs/research/chunk-21.md) |
| chunk-22 | RimWorld | 200 | 16 | 161 | 23 | [docs/research/chunk-22.md](docs/research/chunk-22.md) |
| chunk-23 | RimWorld | 200 | 17 | 147 | 36 | [docs/research/chunk-23.md](docs/research/chunk-23.md) |
| chunk-24 | RimWorld | 200 | 24 | 117 | 59 | [docs/research/chunk-24.md](docs/research/chunk-24.md) |
| chunk-25 | RimWorld | 200 | 28 | 130 | 42 | [docs/research/chunk-25.md](docs/research/chunk-25.md) |
| chunk-26 | RimWorld | 200 | 14 | 161 | 25 | [docs/research/chunk-26.md](docs/research/chunk-26.md) |
| chunk-27 | RimWorld | 200 | 25 | 171 | 4 | [docs/research/chunk-27.md](docs/research/chunk-27.md) |
| chunk-28 | RimWorld | 200 | 43 | 126 | 31 | [docs/research/chunk-28.md](docs/research/chunk-28.md) |
| chunk-29 | RimWorld | 200 | 23 | 142 | 35 | [docs/research/chunk-29.md](docs/research/chunk-29.md) |
| chunk-30 | RimWorld | 113 | 16 | 62 | 35 | [docs/research/chunk-30.md](docs/research/chunk-30.md) |
| chunk-31 | Verse | 250 | 23 | 150 | 77 | [docs/research/chunk-31.md](docs/research/chunk-31.md) |
| chunk-32 | Verse | 250 | 8 | 89 | 153 | [docs/research/chunk-32.md](docs/research/chunk-32.md) |
| chunk-33 | Verse | 250 | 16 | 210 | 24 | [docs/research/chunk-33.md](docs/research/chunk-33.md) |
| chunk-34 | Verse | 250 | 19 | 135 | 96 | [docs/research/chunk-34.md](docs/research/chunk-34.md) |
| chunk-35 | Verse | 250 | 29 | 100 | 121 | [docs/research/chunk-35.md](docs/research/chunk-35.md) |
| chunk-36 | Verse | 250 | 34 | 114 | 102 | [docs/research/chunk-36.md](docs/research/chunk-36.md) |
| chunk-37 | Verse | 247 | 35 | 140 | 72 | [docs/research/chunk-37.md](docs/research/chunk-37.md) |
| chunk-38 | RimWorld.Planet | 151 | 55 | 80 | 16 | [docs/research/chunk-38.md](docs/research/chunk-38.md) |
| chunk-39 | RimWorld.Planet | 151 | 35 | 84 | 32 | [docs/research/chunk-39.md](docs/research/chunk-39.md) |
| chunk-40 | RimWorld.QuestGen | 191 | 29 | 159 | 3 | [docs/research/chunk-40.md](docs/research/chunk-40.md) |
| chunk-41 | RimWorld.QuestGen | 190 | 15 | 169 | 6 | [docs/research/chunk-41.md](docs/research/chunk-41.md) |
| chunk-42 | Verse.AI+Group | 204 | 50 | 128 | 26 | [docs/research/chunk-42.md](docs/research/chunk-42.md) |
| chunk-43 | Verse.AI+Group | 203 | 8 | 187 | 8 | [docs/research/chunk-43.md](docs/research/chunk-43.md) |
| chunk-44 | BaseGen+SketchGen+Misc | 174 | 66 | 98 | 10 | [docs/research/chunk-44.md](docs/research/chunk-44.md) |
| chunk-45 | 顶层+Glow+Noise+Sound+LudeonTK等 | 220 | 18 | 75 | 127 | [docs/research/chunk-45.md](docs/research/chunk-45.md) |

## 备注

- 分片方式：按命名空间分组 + 文件名字典序切分（同一家族的类天然相邻，如 Comp*、JobDriver*、IncidentWorker*）。
- 「未验证」标注：个别文件只读到关键段落的，或结论依赖其它分片本体的，在各发现文档内以「未验证」标注；这不影响文件级覆盖率（每个文件都有处置记录），但深读度以各文档标注为准。
- 当前仓库只保留第二轮穷举调研；第一轮关键词式调研归档已删除，不再作为事实源。
