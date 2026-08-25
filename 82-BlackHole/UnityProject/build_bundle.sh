#!/bin/bash
# 构建 BlackHole shader AssetBundle（需要 Unity 许可证已激活）
set -e
export LD_LIBRARY_PATH=$HOME/unity-libs/root/usr/lib/x86_64-linux-gnu
EDITOR_BIN=$HOME/Unity/Hub/Editor/2022.3.62f2/Editor/Unity
cd "$(dirname "$0")"
"$EDITOR_BIN" -batchmode -quit -projectPath . -executeMethod BuildBlackHoleBundle.Build -logFile ../UnityBuild/unity_build.log
