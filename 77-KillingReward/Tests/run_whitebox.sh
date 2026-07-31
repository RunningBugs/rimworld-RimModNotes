#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/whitebox"
python3 -m unittest -v test_killingreward_static
