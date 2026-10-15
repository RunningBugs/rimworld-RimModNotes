# 18-HakuroXenohumanZh：1.7 升级

2026-10-15；游戏1.7.4977 rev1248。静态语言适配；配套种族依赖未安装，实际注入验证未完成。

- 1.7语言内容从1.4复制，旧1.4及模板源码不改；仅修正star Body.xml重复XML声明/声明前空白，全部语言XML解析通过。
- 新增1.7支持标签，无C#业务或新DLL。

旧版所有文件/ID保留，本轮有完整备份：expansion/backup.json。改动先在stage完成，主代理受控回写，不提交推送。

原生用例：未运行：缺hakuro.xenohuman.septentrionrace；静态XML不代表翻译有效覆盖。结果/原始命令、boot及断言见testing/cases。未覆盖跨版本存档/任意第三方组合。


## 主代理最终覆盖证据（2026-10-15）

前段编译/待运行说明属于代理阶段；以下为最终实测覆盖，未列工作流、物理交互、其它三方组合与跨1.6存档仍保留边界。
当前只完成静态1.7适配，缺种族依赖，未实际验证翻译注入。

四个本地fork及整个RimModNotes/工作区完整备份验证已完成，见expansion/backup.json；旧版/ID/原用户改动保留。所有游戏测试完全隔离，未修改用户配置/存档，未提交/推送。主代理将在整合后核验受控回写哈希与实际本地路径。


主代理已回写至实际本地目录：/home/lisanhu/mine/workspace/rimworld/RimModNotes/18-HakuroXenohumanZh。本项仍只做静态1.7翻译适配，实际缺前置的注入边界不变。原始文件保留与显式清单哈希均复验；本轮未commit/push/Workshop发布。完整结果见 /home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/expansion/REPORT-1.7.md。
