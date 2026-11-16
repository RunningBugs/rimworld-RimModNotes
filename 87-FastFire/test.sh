#!/usr/bin/env bash
set -euo pipefail
FF_PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
bash "$FF_PROJECT_DIR/build.sh"
export NUGET_PACKAGES="$FF_PROJECT_DIR/.build/packages"
export DOTNET_CLI_HOME="$FF_PROJECT_DIR/.build/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet build "$FF_PROJECT_DIR/Tests/Integration.csproj" -c Release --configfile "$FF_PROJECT_DIR/NuGet.Config"
CASE_ARG="${1:-headless-$(date +%s)}"
if [ $# -gt 0 ]; then shift; fi
python3 "$FF_PROJECT_DIR/Tests/run_game.py" "$CASE_ARG" "$@"
