# 40-AreaUnlockerReorderFix：1.7 升级

2026-10-15；游戏1.7.4977 rev1248。原生AreaManager补丁通过，缺少AreaUnlocker完整组合验证。

- 新1.7/net10固定Ref1.7.4977-alpha.1与Harmony3.0.0-rc.2，移除未使用HugsLib；显式PatchAll自身程序集，原prefix行为保留。
- 真实1.7 AreaManager.SortAreas仍为private且被TryMakeNewAllowed调用；未使用Publicizer。原生排序抑制、单独卸本prefix的真实排序负控以及finally恢复通过。

旧版所有文件/ID保留，本轮有完整备份：expansion/backup.json。改动先在stage完成，主代理受控回写，不提交推送。

构建：scripts/dotnet.sh build expansion/work/40-AreaUnlockerReorderFix/1.7/Source/mod.csproj -c Release --configfile .nuget/nuget.config；0 warning/0 error。完整静态程序集引用另存expansion/logs/40-AreaUnlockerReorderFix-api.json；私有Harmony目标逐项源码核实不只依赖MemberRef。

原生用例：expansion-small-37-40。结果/原始命令、boot及断言见testing/cases。未覆盖跨版本存档/任意第三方组合。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-small-37-40：20.36秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/40-AreaUnlockerReorderFix。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
