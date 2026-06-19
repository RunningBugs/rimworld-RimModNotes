#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
main = (ROOT / "1.6/Source/Main.cs").read_text()
native = (ROOT / "1.6/Source/linux_ime_native.c").read_text()

checks = []

def check(name, cond):
    if not cond:
        raise AssertionError(name)
    checks.append(name)

check("C# requires full native ABI", "Missing required native exports" in main and "pIsReady != null" in main and "pFocusOut != null" in main)
check("C# uses frame-seen focus edge flag", "sawFocusedTextFieldThisFrame" in main and "if (!sawFocusedTextFieldThisFrame)" in main)
check("C# resets old focus on control switch", "focusedControl != 0" in main and "NativeBridge.Reset();" in main and "NativeBridge.FocusOut();" in main)
check("C# cursor uses caret offset, not fixed xMin+8", "CursorScreenPoint" in main and "CaretXOffset(editor)" in main)
check("C# preserves late commit even when ProcessKey is false", re.search(r"if \(!string\.IsNullOrEmpty\(commit\)\).*?state\.Commit = commit", main, re.S) is not None)
check("C# renderer restores matrix/color but keeps top depth", "GUI.depth = -10000" in main and "GUI.depth = oldDepth" not in main and "GUI.matrix = oldMatrix" in main and "Text.Font = oldFont" in main)

check("native no-reply helper is used for focus/cursor", "dbus_send_no_reply(conn, \"org.freedesktop.IBus\", ic_path" in native)
check("native no-reply helper does not flush synchronously", "dbus_connection_flush(c)" not in native)
check("native matches UpdatePreeditTextWithMode", "UpdatePreeditTextWithMode" in native)
check("native UTF-8 safe truncation helper used", native.count("utf8_safe_prefix_len") >= 5)
check("native init fast path requires conn and ic_path", "if (conn && ic_path) return 1" in native)
check("native cleanup on partial init failure", "cleanup_ibus(); return 0" in native)

print(f"source_invariant_tests: PASS ({len(checks)} checks)")
