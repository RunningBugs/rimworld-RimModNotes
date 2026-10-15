# 37-PolarisblocFoodToDrugPatch：1.7 升级

2026-10-15；游戏1.7.4977 rev1248。真实SF食物与DrugPolicy验证通过。

- 只追加1.7元数据，现有Common Patch/Data原字节保留；实际OrderedTakeGroupDef Drug、Medical类别、CompProperties_Drug与当前API/XML合同一致。
- 真实SF食物在新DrugPolicy中可配置携带，普通vanilla MealSurvivalPack为非药物负控。Mechenemy分支/文化与实际食用仍未测。

旧版所有文件/ID保留，本轮有完整备份：expansion/backup.json。改动先在stage完成，主代理受控回写，不提交推送。

原生用例：expansion-small-37-40。结果/原始命令、boot及断言见testing/cases。未覆盖跨版本存档/任意第三方组合。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
- expansion-small-37-40：20.36秒，正常exit0/未超时，相关实际检查通过；仅原版Headless 0x0 resolution基线ERROR。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/37-PolarisblocFoodToDrugPatch。回写后 expansion-local-selfuse-final 使用实际本地新1.7路径再次验证，相关检查通过且正常退出、无超时；Headless仅基线0x0 ERROR，渲染组合无ERROR。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
