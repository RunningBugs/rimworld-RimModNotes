# AGENTS.md

- 支持 RimWorld 1.6 与 1.7 beta；保留独立版本目录。1.6参考源码：`~/mine/workspace/rimworld/RW/16/Source`；1.7为真实固定快照 `~/mine/workspace/rimworld/RW/17/Source`。
- 构建：`cd 1.6/Source && dotnet build CarryPolicy.csproj -c Release` → `1.6/Assemblies/CarryPolicy.dll`。
  旧1.6 NuGet保持 `Krafs.Rimworld.Ref 1.6.*`、`Lib.Harmony 2.3.1.1`。1.7按用户本轮beta授权，独立 `1.7/Source/CarryPolicy.csproj` net10.0，锁定 `Krafs.Rimworld.Ref 1.7.4977-alpha.1`、`Lib.Harmony 3.0.0-rc.2`；只在项目隔离cache构建，不改全局环境。
- 命名空间 `_CarryPolicy`（防与类名 CarryPolicy 撞）。
- 结构：`About/`（元数据）、`Common/`（Defs/Patches/双语 Languages，1.5+ 自动加载）、
  `1.6/Assemblies`（产物）、`1.6/Source`（源码）、`sim/`（imgui-sim UI 原型，不参与游戏加载）。
- 修改 C# 后必须重新构建 DLL；修改 XML/语言文件不需要。
- UI 原型：`sim/carry_policy_dialog_sim.py`，用
  `~/mine/workspace/rimworld/rimworld-imgui-sim/.venv/bin/python` 运行，图在 `sim/out/`。
  `vision_review.py` 需要 `OPENROUTER_API_KEY` 环境变量（视觉审查）。
- 1.6 注意：`ThingDef.category` 是 `ThingCategory` 枚举（`ThingCategory.Item`），
  不是旧版 `ThingCategoryDefOf.Item`；结束当前 Job 用 `pawn.jobs.curDriver.EndJobWith(...)`。
