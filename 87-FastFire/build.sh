#!/usr/bin/env bash
set -euo pipefail
FF_PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
export NUGET_PACKAGES="$FF_PROJECT_DIR/.build/packages"
export NUGET_HTTP_CACHE_PATH="$FF_PROJECT_DIR/.build/http-cache"
export NUGET_SCRATCH="$FF_PROJECT_DIR/.build/scratch"
export DOTNET_CLI_HOME="$FF_PROJECT_DIR/.build/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
FF_DOTNET="${FF_DOTNET:-dotnet}"
for FF_VERSION in 1.6 1.7; do
  "$FF_DOTNET" restore "$FF_PROJECT_DIR/$FF_VERSION/Source/FastFire.csproj" --configfile "$FF_PROJECT_DIR/NuGet.Config" "$@"
  "$FF_DOTNET" build "$FF_PROJECT_DIR/$FF_VERSION/Source/FastFire.csproj" -c Release --no-restore
done
python3 "$FF_PROJECT_DIR/tools/prepare_workshop.py"
