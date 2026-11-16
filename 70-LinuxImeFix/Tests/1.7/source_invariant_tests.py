#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
main = (ROOT / "1.7/Source/Main.cs").read_text()
native = (ROOT / "1.7/Source/linux_ime_native.c").read_text()

checks = []

def check(name, cond):
    if not cond:
        raise AssertionError(name)
    checks.append(name)

check("C# requires full native ABI", "Missing required native exports" in main and "pIsReady != null" in main and "pFocusOut != null" in main)
check("C# uses frame-seen focus edge flag", "sawFocusedTextFieldThisFrame" in main and "if (!sawFocusedTextFieldThisFrame" in main)
check("C# resets old focus on control switch", "focusedControl != 0" in main and "NativeBridge.Reset();" in main and "NativeBridge.FocusOut();" in main)
check("C# cursor uses caret offset, not fixed xMin+8", "CursorScreenPoint" in main and "CaretXOffset(editor)" in main)
check("C# preserves late commit even when ProcessKey is false", re.search(r"if \(!string\.IsNullOrEmpty\(commit\)\).*?state\.Commit = commit", main, re.S) is not None)
check("C# keeps IBus focus across non-TextField OnGUI passes while keyboardControl still owns TextEditor", "GUIUtility.keyboardControl == focusedControl" in main and "clears IBus/Rime preedit immediately" in main)
check("C# candidate overlay follows warmed Waylith dispatch, without a second OnGUI renderer", "RimWorldWaylith.Imgui.ImguiDispatch" in main and '"Dispatch", new[] { typeof(Vector2), typeof(bool) }' in main and "CandidateWindowRenderer" not in main and "GUI.depth" not in main)
check("C# candidate overlay draws only repaint and restores scale matrix/color/font", "Event.current?.rawType != EventType.Repaint" in main and "new Vector3(Prefs.UIScale, Prefs.UIScale, 1f)" in main and "GUI.matrix = oldMatrix" in main and "GUI.color = oldColor" in main and "Text.Font = oldFont" in main)
check("C# final focus loss invalidates an anchor even if the field was seen earlier", "!sawFocusedTextFieldThisFrame || GUIUtility.keyboardControl != focusedControl" in main)
check("C# overlay also requires Waylith's actual drawing pass", "public static void Postfix(bool __1)" in main and "if (__1) LinuxImeUtility.DrawCandidateWindow();" in main)
check("C# candidate diagnostic is bounded and does not log candidate text", "overlayRepaintLogs < 3" in main and 'candidates={candidates.Count}' in main)

check("native no-reply helper is used for focus/cursor", "dbus_send_no_reply(conn, \"org.freedesktop.IBus\", ic_path" in native)
check("native no-reply helper does not flush synchronously", "dbus_connection_flush(c)" not in native)
check("native matches UpdatePreeditTextWithMode", "UpdatePreeditTextWithMode" in native)
check("native UTF-8 safe truncation helper used", native.count("utf8_safe_prefix_len") >= 5)
check("native init fast path requires conn and ic_path", "if (conn && ic_path) return 1" in native)
check("native cleanup on partial init failure", "cleanup_ibus(); return 0" in native)

print(f"source_invariant_tests: PASS ({len(checks)} checks)")
