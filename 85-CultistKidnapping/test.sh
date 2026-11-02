#!/usr/bin/env bash
set -euo pipefail
CK_PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
export NUGET_PACKAGES="$CK_PROJECT_DIR/.build/packages"
export DOTNET_CLI_HOME="$CK_PROJECT_DIR/.build/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
CK_DOTNET="${CK_DOTNET:-dotnet}"
"$CK_PROJECT_DIR/build.sh" --locked-mode
"$CK_DOTNET" build "$CK_PROJECT_DIR/Tests/Integration.csproj" -c Release --configfile "$CK_PROJECT_DIR/NuGet.Config"
if [[ ! -x "$CK_PROJECT_DIR/.venv/bin/python" ]]; then uv venv "$CK_PROJECT_DIR/.venv"; fi
"$CK_PROJECT_DIR/.venv/bin/python" "$CK_PROJECT_DIR/Tests/run_game.py" "$@"
