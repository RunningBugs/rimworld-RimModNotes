## RimWorld 1.7 beta

保留1.6，新增 `1.7/Source` net10.0，产物 `1.7/Assemblies/LandingOnAsteroid.dll`。使用固定 Krafs.Rimworld.Ref 1.7.4977-alpha.1 / Lib.Harmony 3.0.0-rc.2；实际小行星地图与起始流程验证范围见 `UPGRADE-1.7.md`。以下历史模板的Framework要求只对应旧版。

# Rimworld Mod Template Monodevelop and Linux

This repository is generated from github template [https://github.com/Rimworld-Mods/Template](https://github.com/Rimworld-Mods/Template)

The original template is targeting Windows and not straightforward for a Linux environment. I have made some changes to make it more friendly to Linux users.

## Pre-requisite
- .NET Framework 4.7.2 SDK installed (recommend using the dotnet-install script from [https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script))
- Install monodevelop (or other tools that support NuGet, I think vscode supports that using the C# Dev Kit plugin)
  - In monodevelop, Go to Tools > Extensions > IDE extensions and install NuGet Package Management Extensions
- Fill mod.csproj with RootNamespace, AssemblyName, and other necessary settings.
- Open mod.csproj using your IDE supporting NuGet, and you should be good to go



__Below is the README from original template, which is no longer valid in this repo. And will be removed in next big release of this repo.__

-------
# ~~Rimworld Mod Template~~

This template is created for Rimworld modders who use [Visual Studio Code](https://code.visualstudio.com/) instead of Visual Studio IDE.

* __No virtual folders__. Easy to manage and edit both `xml` and `cs` files.

* __Lightweight__. Visual Studio Code only takes up to 200 MB of storage space and is lighting fast.

* __Automated__. Integrated build, scripting and management tools to perform common tasks making everyday workflows faster.

* __Customizable__. Almost every feature can be changed, whenever it is editor UI, keybinds or folder structure.

## Setup
1. Download and install [.NET Core SDK](https://dotnet.microsoft.com/download/dotnet-core) and [.Net Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48). This step can be skipped if you already have required C# packages from Visual Studio IDE.
2. Install [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp).
3. Clone, pull or download this template into your Rimworld `Mods` folder.

## Additional notes
* By pressing `F5` key VS Code will perform 2 operations: build assembly file and launch Rimworld executable. 
* All intermediate files are kept inside `.vscode` folder.
* For XML only modders remove preLaunchTask line from `.vscode/launch.json` file.
* Modify `.vscode/mod.csproj` and `About/About.xml` according to your needs.

