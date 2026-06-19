#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== build native =="
gcc -shared -fPIC -O2 -Wall -Wextra \
  -o 1.6/Assemblies/libLinuxImeFixNative.so \
  1.6/Source/linux_ime_native.c \
  -ldbus-1 -lpthread $(pkg-config --cflags dbus-1)

echo "== build C# =="
~/.dotnet/dotnet build 1.6/Source/mod.csproj -v:minimal

echo "== native whitebox =="
gcc -Wall -Wextra -O0 -g -o tests/native_whitebox_tests \
  tests/native_whitebox_tests.c \
  -ldbus-1 -lpthread $(pkg-config --cflags dbus-1)
./tests/native_whitebox_tests

echo "== source invariants =="
python3 tests/source_invariant_tests.py

echo "== export ABI =="
python3 - <<'PY'
import ctypes
from pathlib import Path
so = Path('1.6/Assemblies/libLinuxImeFixNative.so').resolve()
lib = ctypes.CDLL(str(so))
exports = [
    'rimworld_ime_init', 'rimworld_ime_process_key', 'rimworld_ime_poll_utf8',
    'rimworld_ime_get_preedit', 'rimworld_ime_get_preedit_cursor', 'rimworld_ime_is_preedit_visible',
    'rimworld_ime_get_candidate_count', 'rimworld_ime_get_candidate', 'rimworld_ime_get_lookup_cursor',
    'rimworld_ime_is_lookup_visible', 'rimworld_ime_focus_in', 'rimworld_ime_focus_out',
    'rimworld_ime_set_cursor', 'rimworld_ime_reset', 'rimworld_ime_is_ready',
]
missing = [name for name in exports if not hasattr(lib, name)]
if missing:
    raise SystemExit(f'missing exports: {missing}')
print(f'export_abi: PASS ({len(exports)} exports)')
PY

echo "== standalone integration smoke =="
gcc -Wall -Wextra -o test_ime test_ime.c -ldl
timeout 10 ./test_ime

echo "== focus latency regression =="
python3 - <<'PY'
import ctypes, time
lib = ctypes.CDLL('./1.6/Assemblies/libLinuxImeFixNative.so')
if lib.rimworld_ime_init() != 1:
    raise SystemExit('init failed')
start = time.monotonic()
for _ in range(10000):
    lib.rimworld_ime_focus_out()
    lib.rimworld_ime_set_cursor(100, 200)
elapsed = time.monotonic() - start
print(f'focus_out_set_cursor_10000_elapsed={elapsed:.3f}s')
if elapsed > 1.0:
    raise SystemExit('focus/cursor no-reply latency regression')
PY

echo "ALL TESTS PASS"
