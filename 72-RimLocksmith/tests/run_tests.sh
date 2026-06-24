#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== whitespace check =="
git diff --check -- .

echo "== XML validation =="
xmllint --noout About/About.xml Common/Languages/English/Keyed/RimLocksmith_Keys.xml Common/Languages/ChineseSimplified/Keyed/RimLocksmith_Keys.xml 1.6/Patches/Core/*.xml

echo "== source invariants =="
python3 tests/source_invariant_tests.py

echo "== build mod =="
~/.dotnet/dotnet build 1.6/Source/mod.csproj -v:minimal

echo "== whitebox tests =="
~/.dotnet/dotnet run --project tests/RimLocksmith.Tests/RimLocksmith.Tests.csproj -v:minimal

echo "ALL TESTS PASS"
