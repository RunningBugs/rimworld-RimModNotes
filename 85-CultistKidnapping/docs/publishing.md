# 创意工坊发布

条目：[邪教徒绑架 Cultist Kidnapping](https://steamcommunity.com/sharedfiles/filedetails/?id=3832488339)，ID 保存在 `About/PublishedFileId.txt`。初次上传返回 `k_EResultOK`，读取服务端详情确认公开、未封禁、不需要另行接受工坊协议。记录见 `publishing-result.json`。

本次审阅了已安装 PublisherPlus（工坊 3183156228）1.7 DLL 的 `PublisherPlus.Data.WorkshopPackage`：`GetConfig` 按相对路径前缀、忽略大小写排除文件和整个子目录，并自动跳过 `_PublisherPlus.xml`；不是通配符规则。新配置与仓库其它 MOD 使用相同格式，排除 `.build/`、`.venv/`、测试、文档、工具及各版 `obj/bin`，保留实际资源、三版成品 DLL、公共源码、项目文件、NuGet 锁文件、构建脚本和许可证。

脚本也核对了原版 `Verse.Steam.Workshop.Upload`、`OnItemCreated` 和 `SetWorkshopItemDataFrom`：通过游戏现有 Steamworks.NET 绑定执行 CreateItem → StartItemUpdate → SetItemTitle/Description/Preview/Tags/Content → SubmitItemUpdate，并等待实际回调。版本标签为 Mod、1.5、1.6、1.7。首次创建后立即保存 ID，后续只更新同一个条目；更新前核对账号所有权和 RimWorld AppID。结果回调后再读取服务端详情，不以请求发出作为成功。

上传内容约 128 KB，24 个选定文件加首次创建后生成的 PublishedFileId.txt。不上传游戏/Harmony 参考 DLL、缓存、原版反编译副本、个人配置或测试存档。游戏 DLL 只由本地发布工具引用，不进入上传目录。

```bash
# 只生成上传目录和文件/哈希清单，不连接 Steam：
.venv/bin/python tools/prepare_upload.py

# 上传或更新公开条目，使用已登录的本机 Steam：
tools/publish.sh
```

`.build/publish/content/` 是实际上传目录，`manifest.json` 记录准备阶段的文件和 SHA-256；初次创建 ID 文件在 Steam CreateItem 成功后补入。`result.json` 保存 Steam 提交和回读结果。需要 .NET 10，首次使用先 `uv venv .venv`；Python 只使用标准库。NuGet/CLI 缓存仍在项目 `.build/` 内。默认游戏路径可用 `CK_GAME_DIR` 覆盖，SDK 可用 `CK_DOTNET` 覆盖。

本地工具和这些发布记录均不随工坊包上传。用户已明确要求直接上传并提供结果链接，本次没有额外的发布确认步骤。
