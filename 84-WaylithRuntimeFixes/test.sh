#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/env.sh"
"$RW_FIX_PROJECT_DIR/build.sh"
RW_FIX_SNAPSHOT="${1:-/home/lisanhu/mine/workspace/rimworld/RW/17/Assemblies}"
"$RW_FIX_DOTNET" restore "$RW_FIX_PROJECT_DIR/Tests/WaylithRuntimeFixes.Tests.csproj" --configfile "$RW_FIX_PROJECT_DIR/NuGet.Config"
"$RW_FIX_DOTNET" build "$RW_FIX_PROJECT_DIR/Tests/WaylithRuntimeFixes.Tests.csproj" --configuration Release --no-restore
"$RW_FIX_DOTNET" "$RW_FIX_PROJECT_DIR/Tests/bin/Release/net10.0/WaylithRuntimeFixes.Tests.dll" "$RW_FIX_SNAPSHOT"
