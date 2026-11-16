#!/usr/bin/env bash
set -euo pipefail
RFS_PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
bash "$RFS_PROJECT_DIR/build.sh"
export NUGET_PACKAGES="$RFS_PROJECT_DIR/.build/packages"
export DOTNET_CLI_HOME="$RFS_PROJECT_DIR/.build/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet build "$RFS_PROJECT_DIR/Tests/Integration.csproj" -c Release --configfile "$RFS_PROJECT_DIR/NuGet.Config"
"$RFS_PROJECT_DIR/.venv/bin/python" "$RFS_PROJECT_DIR/Tests/run_game.py" "$@"
