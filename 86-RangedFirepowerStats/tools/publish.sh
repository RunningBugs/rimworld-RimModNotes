#!/usr/bin/env bash
set -euo pipefail
RFS_PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
RFS_GAME_DIR="${RFS_GAME_DIR:-/Data/SteamLibrary/steamapps/common/RimWorld}"
export NUGET_PACKAGES="$RFS_PROJECT_DIR/.build/packages"
export DOTNET_CLI_HOME="$RFS_PROJECT_DIR/.build/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
"$RFS_PROJECT_DIR/.venv/bin/python" "$RFS_PROJECT_DIR/tools/prepare_workshop.py"
dotnet build "$RFS_PROJECT_DIR/tools/Publisher/Publisher.csproj" -c Release --configfile "$RFS_PROJECT_DIR/NuGet.Config" -p:RimWorldDir="$RFS_GAME_DIR"
mkdir -p "$RFS_PROJECT_DIR/.build/publish"
cd "$RFS_PROJECT_DIR/.build/publish"
dotnet "$RFS_PROJECT_DIR/.build/publisher/bin/net10.0/Publisher.dll" "$RFS_PROJECT_DIR" "$RFS_GAME_DIR" "$@"
