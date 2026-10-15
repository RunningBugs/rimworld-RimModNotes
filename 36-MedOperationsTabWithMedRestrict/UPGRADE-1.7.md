> 主代理游戏验证：`mod36-rev1248-m`，进程/加载检查 通过，真实功能断言 通过。证据：`/home/lisanhu/mine/workspace/rimworld/tmp/update-1-7/testing/cases/mod36-rev1248-m`。与原版基线相同的无窗口 0×0 分辨率错误不计作 MOD 新增错误。验证范围以断言及下文限制为准。

# RimWorld 1.7 升级：Operations Tab With Med Restrict

packageId `RunningBugs.OperationsTabWithMedRestrict`，Workshop `3417248769`。以当前1.6为基线，审查实际 `1.7.4977 rev1248` 的 HealthCardUtility/MedicalCareUtility/Pawn与新版UnityStubs。**已适配、编译通过，真实功能断言待主代理执行**。

新增1.7源码/同名 `OperationsTabWithMedRestrict.dll` 和About版本标签；生产仅把PatchAll改为明确 `typeof(Start).Assembly`。新工程net10.0、精确 `Krafs.Rimworld.Ref 1.7.4977-alpha.1` / `Lib.Harmony 3.0.0-rc.2`，去掉未用HugsLib；无直接非公开成员访问，不需要Publicizer。旧版/元数据身份保持，默认自动1.7/Common/root，.bak不是活跃LoadFolders；Common Def/Patch/语言只有注释或空根，无需内容迁移。

## API 审查

完整原/新静态metadata为 `logs/36-MedOperationsTabWithMedRestrict/api-{original,new}.json`，唯一自有业务函数Prefix和启动均审查。

- Harmony目标仍是private static `HealthCardUtility.DrawMedOperationsTab(Rect leftRect,Pawn pawn,Thing thingForMedBills,float curY) -> float`；真实DrawHealthCard调用该方法，新增Prefix `(Rect leftRect,Pawn pawn,ref float curY)`通过原参数名改变传入native布局，未失联。原native继续绘制手术列表。
- 对比真实1.7 DrawOverviewTab，同一药物UI分支的医疗适用条件仍相同：Flesh、玩家Faction或HostFaction/可医疗床上野人与动物、Mutant.entitledToMedicalCare、playerSettings存在、未Dead且ProgramState.Playing。本轮不简化复杂条件、扩大类型范围或改变23+4像素行。
- Text.Font/Anchor、GUI.color、Rect切分、TooltipHandler/Widgets.Label/DrawHighlight/DrawButtonGraphic均保留；真正按钮仍调用原生 `MedicalCareUtility.MedicalCareSelectButton(Rect,Pawn)`，其DropDown直接用`pawn.playerSettings.medCare` getter/menu setter，并有5个policy+ChangeDefaults；原生AllowsMedicine消费者按Herbal/Industrial/Ultratech药效限制，未出现“只改UI不改实际字段”。
- Pawn.Faction/HostFaction、RaceProps.IsFlesh、NonHumanlikeOrWildMan/InBed/CurrentBed、Mutant、Dead、playerSettings与医疗类别枚举合同保留。无第三方接口、DefOf、transpiler、反射入口或自有保存字段。

## 验证

```bash
scripts/dotnet.sh restore work/36-MedOperationsTabWithMedRestrict/1.7/Source/mod.csproj --configfile .nuget/nuget.config
scripts/dotnet.sh build work/36-MedOperationsTabWithMedRestrict/1.7/Source/mod.csproj -c Release --no-restore
scripts/dotnet.sh build logs/36-MedOperationsTabWithMedRestrict/Checks.csproj -c Release --no-restore
```

生产、测试工程0warning/0error，输出只有本MOD DLL/deps.json；静态XML/旧版本字节/布局检查 `static-validation.json`。

没有原单测或可独立BCL业务：Prefix全部依赖真实Pawn/游戏/UI上下文，故不造模拟条件函数作复刻单测。`Tests/1.7/Mod36Checks.cs`编译通过，真实运行尚待主代理：

- 调用真实patched DrawMedOperationsTab入口，测试专用Priority.Last prefix只跳过后续原生手术列表body并回传已修改curY，保留本MOD prefix实际执行；临时MedicalCareSelectButton observer只截GPU下拉入口，观察真实正确pawn/Rect和新增27px。
- 从该真实pawn调用原生menu generator，执行全部5个实际action，断言其真实playerSettings.medCare改变且原生AllowsMedicine对3档真实药品给出正确权限。specific unpatch本MOD prefix作为阴性控制后精确恢复，不用空驱动只检查注册。
- 非Playing/nullsettings/真实Dead枚举/非玩家human/实际玩家hosted guest/可选Biotech非Flesh机体负正控制；finally清理各具体测试patch、生成pawn、字体/GUI/Event/ProgramState。没有用户设置/保存写入。

**仍待视觉/交互验证**：菜单像素、布局滚动、鼠标真实点击/拖拽、整个手术列表与药物选择联动及真实手术job、床上动物/野人、Mutant允许/拒绝、跨MOD Health UI、原生policy保存重载。拦截GPU和native后续body的输入断言不代表上述内容已通过。
