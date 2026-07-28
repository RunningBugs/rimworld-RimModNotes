# 任务清单

- [x] 建立 `76-ColonyGroupsTargetablePortraits/` 目录结构（About、1.6/Assemblies、1.6/Source）
- [x] 编写 `About/About.xml`（supportedVersions 1.6，loadAfter harmony + Colony Groups）
- [x] 拷贝 `TacticalGroups.dll` 至 `1.6/Source/`（编译期引用，不分发）
- [x] 编写 `mod.csproj` / `mod.sln`（net472，Publicizer，引用 TacticalGroups Private=false）
- [x] 编写 Harmony patch（ColonistBar.TryGetEntryAt prefix：主栏 TryGetEntryAt → 弹窗 TryGetGroupPawnAt）
- [x] `dotnet build` 编译通过，DLL 输出到 `1.6/Assemblies/`
- [x] 软链接到游戏 MOD 目录
- [ ] 游戏内验证（proposal.md 验证清单 1-6，含 Player.log 检查）
- [ ] spec-flow 归档（验证通过后）
