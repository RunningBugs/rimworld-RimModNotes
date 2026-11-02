#!/usr/bin/env bash
set -euo pipefail
CK_PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
export NUGET_PACKAGES="$CK_PROJECT_DIR/.build/packages"
export NUGET_HTTP_CACHE_PATH="$CK_PROJECT_DIR/.build/http-cache"
export NUGET_SCRATCH="$CK_PROJECT_DIR/.build/scratch"
export DOTNET_CLI_HOME="$CK_PROJECT_DIR/.build/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
CK_DOTNET="${CK_DOTNET:-dotnet}"
for CK_VERSION in 1.5 1.6 1.7; do
  "$CK_DOTNET" restore "$CK_PROJECT_DIR/$CK_VERSION/Source/CultistKidnapping.csproj" --configfile "$CK_PROJECT_DIR/NuGet.Config" "$@"
  "$CK_DOTNET" build "$CK_PROJECT_DIR/$CK_VERSION/Source/CultistKidnapping.csproj" -c Release --no-restore
done
