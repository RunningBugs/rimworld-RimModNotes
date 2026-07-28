# 任务清单

- [ ] 建立 `76-ColonyGroupsTargetablePortraits/` 目录结构（About、1.6/Assemblies、1.6/Source）
- [ ] 编写 `About/About.xml`（supportedVersions 1.6，loadAfter harmony + Colony Groups）
- [ ] 拷贝 `TacticalGroups.dll` 至 `1.6/Source/`（编译期引用，不分发）
- [ ] 编写 `mod.csproj` / `mod.sln`（net472，Publicizer，引用 TacticalGroups Private=false）
- [ ] 编写 Harmony patch（ColonistBar.TryGetEntryAt prefix：主栏 TryGetEntryAt → 弹窗 TryGetGroupPawnAt）
- [ ] `dotnet build` 编译通过，DLL 输出到 `1.6/Assemblies/`
- [ ] 软链接到游戏 MOD 目录
- [ ] 游戏内验证（proposal.md 验证清单 1-5）
