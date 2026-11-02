#!/usr/bin/env bash
set -euo pipefail
CK_PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
CK_GAME_DIR="${CK_GAME_DIR:-/Data/SteamLibrary/steamapps/common/RimWorld}"
export NUGET_PACKAGES="$CK_PROJECT_DIR/.build/packages"
export DOTNET_CLI_HOME="$CK_PROJECT_DIR/.build/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
"$CK_PROJECT_DIR/.venv/bin/python" "$CK_PROJECT_DIR/tools/prepare_upload.py"
"${CK_DOTNET:-dotnet}" build "$CK_PROJECT_DIR/tools/Publisher/Publisher.csproj" -c Release --configfile "$CK_PROJECT_DIR/NuGet.Config" -p:RimWorldDir="$CK_GAME_DIR"
cd "$CK_PROJECT_DIR/.build/publish"
"${CK_DOTNET:-dotnet}" "$CK_PROJECT_DIR/tools/Publisher/bin/Release/net10.0/Publisher.dll" "$CK_PROJECT_DIR" "$CK_GAME_DIR"
