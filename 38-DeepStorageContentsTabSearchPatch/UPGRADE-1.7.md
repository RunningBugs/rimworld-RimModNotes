# 1.7 检查：未更新（当前Headless上游基线阻塞）

目标 `1.7.4977 rev1248`。**当前结论 blocked，不部署1.7支持标签或DLL；root只回写本记录**。下面编译/源码工作仅保留于暂存副本以便上游更新后继续，不代表可用。保留原1.5/Source/DLL/Common/ID；独立1.7和About支持，net10.0/精准Krafs.Rimworld.Ref1.7.4977-alpha.1、Lib.Harmony3.0.0-rc.2，显式PatchAll程序集，未用HugsLib模板移除、无需Publicizer。新工程把拼错的PathHint改成MSBuild实际HintPath，使用当前安装真实LWM.DeepStorage.dll Private=false，不复制到发布Assemblies。

## API 与真实语义

实际上游lwm.deepstorage（Workshop1617282896）只到1.5，默认回退1.5+根；不能用旧标签判断不能工作，但其旧binary风险较大，需要root先baseline。实际依赖/sha见dependencies.json。root/Assemblies有IHoldMultipleThings.dll；0MultiplayerAPI.dll只在未选中的1.4/Assemblies，实际1.5DLL含Multiplayer.API引用/属性，需baseline验证游戏解析能否取得API。不要伪造API桩或者修改Workshop来掩盖缺失。

真正FillTab在ITab_DeepStorage_Inventory:76，仍通过ITab.SelThing取当前Building_Storage；五个反射field buildingStorage/size/scrollViewHeight/scrollPosition/ambientTemp、公有labelKey、private DrawThingRow(ref float,float,Thing)、SelThing继承property仍存在（选定源码留log）。CompDeepStorage.GetContentsHeader 实际是ITab_Inventory_HeaderUtil extension，非直接成员；该上游helper从实际建筑AllSlotCells和map.thingGrid取Thing列表，生成容量/重量/reservation Header，是实际输入链。GenericContentsHeader同样存在。

新生产保留全部原UI逻辑，只显式PatchAll。QuickSearchWidget/公开filter.Text、GUI分组/Font/Anchor、Widgets滚动列表/标签、QualityUtility.TryGetQuality(out)、Sort/HitPoints/Label、Event.current Layout都在真实1.7仍支持。源码对照最新UnityStubs/GUI，输入/字体变化仍需实际窗口验证。按Thing.Label.Contains(filter.Text)执行原有大小写敏感搜索，不顺手换成不同搜索语义；private DrawThingRow会更新ref y，reflection parameters[0]返回仍正确。完整静态引用api-references.json，动态field/property/method另单独检查；无自有Scribe字段/非空Def XML迁移。

## 验证

生产build.log 0warning/error；`bash scripts/dotnet.sh build work/38-DeepStorageContentsTabSearchPatch/1.7/Source/mod.csproj -c Release --no-restore`。Map场景 checks-build.log同样通过。

Tests/1.7/Mod38Checks只引用game/Harmony，实际外部assembly反射：检查具体FillTab Prefix注册及真实private字段/方法，创建真正带CompDeepStorage的已加载建筑、实际spawn两种物品到其slot，调用实际nonempty GetContentsHeader并要求返回两种Thing和非空header，清理生成objects。**不调用OnGUI来伪造UI测试**，不以注册/header成功假报过滤或渲染成功。单元测试没有独立有意义算法，GUI需要实际生命周期，所以不写实现镜像测试。

root递归runtime IDs brrainz.harmony,lwm.deepstorage，真实路径 `/Data/SteamLibrary/steamapps/workshop/content/294100/1617282896`；先baseline解析IHoldMultipleThings/MultiplayerAPI，若真正缺失/接口不兼容须记录blocked实际证据。待真实窗口：打开非空deepstorage内容页，搜索对应条目/清空/无结果、滚动/排序/质量/血量/温度tooltip/drop按钮仍正常，切建筑/reset、保存重载、多UImod/新版IME组合。尚未运行游戏或修改真实配置/Workshop。

## 已证实的上游启动阻塞

游戏rev1248，root串行实跑两次：

- `testing/cases/deps38-rev1248-m`：Harmony+实际LWM+测试驱动（**未启用本补丁**），加载日志明确进入LWM ModInit/Settings.MakeAllStorageDeepStorage，之后native SIGSEGV(-11)，没有boot.json，未执行本MOD任何检查。
- `testing/cases/deps38-standalone-boot`：**只启用Harmony与实际LWM，不带本补丁、不带测试驱动、不加quicktest**，18.89秒再现native SIGSEGV(-11)。因此排除own补丁/测试夹具/map快速生成是必要因素，当前实际依赖组合在Headless模式无法完成启动；这不证明有窗口模式也不能使用。root正在专用Xvfb/Vulkan环境另行验证有窗口兼容性。

精确原生根因未知，不能凭最后日志或发现的0MultiplayerAPI路径断言某个缺失DLL一定是崩溃原因。ModInit源码在DefsLoaded之后使用旧无参Harmony.PatchAll，旧generated/私有API补丁与当前运行时均存在风险，但没有足够证据指认具体一项。未修改LWM、Workshop或尝试桩，不越范围升级第三方。

root只应用本UPGRADE记录，绝不应用暂存About支持/1.7DLL；所有临时编译和已编译场景留作后续材料。恢复条件：实际LWM上游与其必要程序集能先在1.7独立完成启动，再继续真实storage/header及窗口搜索交互验证。

root进一步使用真正隔离X11（env -u WAYLAND_DISPLAY、SDL_VIDEODRIVER=x11）正常Vulkan窗口测试：`deps38-rendered-x11` 25.39秒同样SIGSEGV(-11)。相同环境原版+Harmony的`rendered-x11-baseline`22.82秒exit0/零ERROR，实际1280×720 boot.png已查看正常主菜单。现有窗口和Headless均独立复现上游启动阻塞，仍不指认精确native根因。
