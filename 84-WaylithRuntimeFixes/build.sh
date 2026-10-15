#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/env.sh"
"$RW_FIX_DOTNET" restore "$RW_FIX_PROJECT_DIR/1.7/Source/WaylithRuntimeFixes.csproj" --configfile "$RW_FIX_PROJECT_DIR/NuGet.Config" --locked-mode
"$RW_FIX_DOTNET" build "$RW_FIX_PROJECT_DIR/1.7/Source/WaylithRuntimeFixes.csproj" --configuration Release --no-restore
