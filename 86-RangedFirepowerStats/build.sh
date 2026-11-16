#!/usr/bin/env bash
set -euo pipefail
RFS_PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
export NUGET_PACKAGES="$RFS_PROJECT_DIR/.build/packages"
export NUGET_HTTP_CACHE_PATH="$RFS_PROJECT_DIR/.build/http-cache"
export NUGET_SCRATCH="$RFS_PROJECT_DIR/.build/scratch"
export DOTNET_CLI_HOME="$RFS_PROJECT_DIR/.build/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet build "$RFS_PROJECT_DIR/1.7/Source/RangedFirepowerStats.csproj" -c Release --configfile "$RFS_PROJECT_DIR/NuGet.Config" "$@"
