#!/usr/bin/env python3
"""
test_signals.py - Capture and dump the raw D-Bus wire format of IBus input
context signals (CommitText, UpdatePreeditText, UpdateLookupTable, etc.)
while typing "ni" through the Rime engine.

We connect directly to the IBus session bus using the address found in
~/.config/ibus/bus/<machine-id>-unix-<display> and use a low-level
dbus message filter so we can introspect the *exact* D-Bus type signature
and nested structure of each signal argument (needed to parse them from C
with libdbus-1).
"""

import os
import re
import sys
import glob
import time
import threading

import dbus
import dbus.lowlevel
import dbus.connection
import dbus.mainloop.glib
from gi.repository import GLib

# --------------------------------------------------------------------------- #
# 1. Discover the IBus bus address from ~/.config/ibus/bus/
# --------------------------------------------------------------------------- #

def find_ibus_address():
    """Return the IBUS_ADDRESS from the newest bus config matching DISPLAY."""
    disp = os.environ.get("DISPLAY", ":0")
    # e.g. ":1" -> "1";  ":0.0" -> "0"
    m = re.match(r":?(\d+)", disp)
    disp_num = m.group(1) if m else "0"
    pattern = os.path.expanduser(
        f"~/.config/ibus/bus/*-unix-{disp_num}")
    files = glob.glob(pattern)
    if not files:
        # fallback: try wayland or any unix
        files = glob.glob(os.path.expanduser(
            "~/.config/ibus/bus/*-unix-*"))
    if not files:
        raise SystemExit("No ibus bus config files found")
    newest = max(files, key=os.path.getmtime)
    addr = None
    with open(newest) as f:
        for line in f:
            if line.startswith("IBUS_ADDRESS="):
                addr = line.split("=", 1)[1].strip()
                break
    if not addr:
        raise SystemExit(f"No IBUS_ADDRESS in {newest}")
    print(f"[ibus] config file : {newest}")
    print(f"[ibus] address     : {addr}")
    return addr


# --------------------------------------------------------------------------- #
# 2. Recursive D-Bus type / value descriptor
# --------------------------------------------------------------------------- #

def describe(value, indent=0):
    """Recursively describe a dbus value: its D-Bus type code and content.

    Returns a list of lines (strings)."""
    pad = "  " * indent
    lines = []

    if isinstance(value, dbus.Struct):
        sig = str(getattr(value, "signature", "?"))
        lines.append(f"{pad}STRUCT sig=({sig}) len={len(value)}")
        for i, item in enumerate(value):
            lines.append(f"{pad}  [{i}]")
            lines.extend(describe(item, indent + 2))
    elif isinstance(value, dbus.Dictionary):
        sig = str(getattr(value, "signature", "?"))
        lines.append(f"{pad}DICT sig={sig} len={len(value)}")
        for k, v in value.items():
            lines.append(f"{pad}  key={describe_oneline(k)}")
            lines.extend(describe(v, indent + 2))
    elif isinstance(value, (dbus.Array, list)):
        sig = str(getattr(value, "signature", "?"))
        elem = sig[1:] if sig and sig[0] == "a" else "?"
        lines.append(f"{pad}ARRAY sig={sig} (elem={elem}) len={len(value)}")
        for i, item in enumerate(value):
            lines.append(f"{pad}  [{i}]")
            lines.extend(describe(item, indent + 2))
    elif isinstance(value, str):
        lines.append(f"{pad}STRING s='{value}'")
    elif isinstance(value, dbus.Boolean) or isinstance(value, bool):
        lines.append(f"{pad}BOOLEAN b={bool(value)}")
    elif isinstance(value, dbus.UInt32):
        lines.append(f"{pad}UINT32 u={int(value)}")
    elif isinstance(value, dbus.Int32):
        lines.append(f"{pad}INT32 i={int(value)}")
    elif isinstance(value, dbus.UInt64):
        lines.append(f"{pad}UINT64 t={int(value)}")
    elif isinstance(value, dbus.Int64):
        lines.append(f"{pad}INT64 x={int(value)}")
    elif isinstance(value, (dbus.Byte, int)):
        lines.append(f"{pad}INT/BYTE ={int(value)}")
    elif isinstance(value, dbus.Double):
        lines.append(f"{pad}DOUBLE d={float(value)}")
    elif isinstance(value, dbus.ObjectPath):
        lines.append(f"{pad}OBJECTPATH o='{value}'")
    elif isinstance(value, dbus.Signature):
        lines.append(f"{pad}SIGNATURE g='{value}'")
    else:
        lines.append(f"{pad}<unknown py={type(value).__name__}> "
                     f"repr={repr(value)[:120]}")
    return lines


def describe_oneline(value):
    try:
        return " | ".join(describe(value, 0)).replace("\n", " ")
    except Exception:
        return repr(value)[:120]


# --------------------------------------------------------------------------- #
# 3. Capture signals with a low-level message filter
# --------------------------------------------------------------------------- #

captured = []          # list of dicts describing each captured signal
capture_lock = threading.Lock()


def make_filter(ic_path):
    ic_path = str(ic_path)

    def _filter(conn, msg):
        try:
            if msg.get_type() != dbus.lowlevel.MESSAGE_TYPE_SIGNAL:
                return dbus.connection.HANDLER_RESULT_NOT_YET_HANDLED
            if msg.get_path() != ic_path:
                return dbus.connection.HANDLER_RESULT_NOT_YET_HANDLED
            iface = msg.get_interface()
            member = msg.get_member()
            # only care about IBus InputContext signals
            if iface != "org.freedesktop.IBus.InputContext":
                return dbus.connection.HANDLER_RESULT_NOT_YET_HANDLED
            sig = msg.get_signature()
            args = msg.get_args_list()
            entry = {
                "member": member,
                "signature": sig,
                "path": str(msg.get_path()),
                "args": args,
            }
            with capture_lock:
                captured.append(entry)
            # Print immediately so we see progress
            print(f"\n=== SIGNAL: {member}  sig=({sig}) ===")
            for i, a in enumerate(args):
                print(f"--- arg[{i}] ---")
                for line in describe(a):
                    print(line)
            sys.stdout.flush()
        except Exception as e:
            print(f"[filter error] {e}", file=sys.stderr)
        return dbus.connection.HANDLER_RESULT_NOT_YET_HANDLED

    return _filter


# --------------------------------------------------------------------------- #
# 4. Main
# --------------------------------------------------------------------------- #

# IBus key constants (subset). keyval for 'n' = 0x006e, 'i' = 0x0069
IBUS_KEY_n = 0x006e
IBUS_KEY_i = 0x0069
IBUS_RELEASE_MASK = 1 << 30  # IBUS_RELEASE_MASK


def main():
    dbus.mainloop.glib.DBusGMainLoop(set_as_default=True)
    addr = find_ibus_address()
    bus = dbus.bus.BusConnection(addr)
    print(f"[ibus] connected as {bus.get_unique_name()}")

    ibus = dbus.Interface(
        bus.get_object("org.freedesktop.IBus", "/org/freedesktop/IBus"),
        "org.freedesktop.IBus")

    ic_path = ibus.CreateInputContext("test_signals_py")
    print(f"[ibus] created input context: {ic_path}")

    ic_obj = bus.get_object("org.freedesktop.IBus", ic_path)
    ic = dbus.Interface(ic_obj, "org.freedesktop.IBus.InputContext")

    # Capabilities: request everything (Preedit, LookupTable, etc.)
    # IBUS_CAP_PREEDIT_TEXT=1, IBUS_CAP_AUXILIARY_TEXT=2,
    # IBUS_CAP_LOOKUP_TABLE=4, IBUS_CAP_FOCUS=8, IBUS_CAP_PROPERTY=16,
    # IBUS_CAP_SURROUNDING_TEXT=32
    caps = 1 | 2 | 4 | 8 | 16 | 32
    ic.SetCapabilities(caps)
    ic.FocusIn()
    print("[ibus] FocusIn + SetCapabilities done")

    # Now set engine (requires focus)
    try:
        ic.SetEngine("rime")
        print("[ibus] engine set to rime")
    except Exception as e:
        print(f"[ibus] SetEngine('rime') failed: {e}")
        # fallback: try ibus bus SetEngine variant / or just continue
        try:
            ibus.SetGlobalEngine("rime")
            print("[ibus] SetGlobalEngine('rime') ok")
        except Exception as e2:
            print(f"[ibus] SetGlobalEngine failed: {e2}")

    # Install the message filter
    filt = make_filter(ic_path)
    bus.add_message_filter(filt)

    # Also add a match rule to be safe
    bus.add_match_string(
        f"type='signal',path='{ic_path}',"
        f"interface='org.freedesktop.IBus.InputContext'")
    print("[ibus] filter + match installed")

    loop = GLib.MainLoop()

    # Schedule typing "n" then "i" with key press+release, then quit.
    def type_key(keyval, keycode=0):
        # press
        handled_press = ic.ProcessKeyEvent(keyval, keycode, 0)
        # release
        ic.ProcessKeyEvent(keyval, keycode, IBUS_RELEASE_MASK)
        return bool(handled_press)

    def step_n():
        print("\n>>>>>>>> TYPING 'n' <<<<<<<<")
        try:
            type_key(IBUS_KEY_n)
        except Exception as e:
            print(f"[type n error] {e}")
        return False

    def step_i():
        print("\n>>>>>>>> TYPING 'i' <<<<<<<<")
        try:
            type_key(IBUS_KEY_i)
        except Exception as e:
            print(f"[type i error] {e}")
        return False

    def step_finish():
        print("\n>>>>>>>> DONE TYPING; flushing for 1.5s <<<<<<<<")
        return False

    def step_quit():
        print("\n>>>>>>>> QUITTING <<<<<<<<")
        loop.quit()
        return False

    # Timings (ms)
    GLib.timeout_add(300, step_n)
    GLib.timeout_add(600, step_i)
    GLib.timeout_add(900, step_finish)
    GLib.timeout_add(2400, step_quit)

    print("[ibus] running mainloop for ~2.4s to capture signals...\n")
    loop.run()

    # Cleanup
    try:
        ic.FocusOut()
    except Exception:
        pass
    try:
        ic.Reset()
    except Exception:
        pass

    # ---- Summary ---------------------------------------------------------- #
    print("\n\n" + "=" * 70)
    print("CAPTURED SIGNAL SUMMARY")
    print("=" * 70)
    with capture_lock:
        for idx, entry in enumerate(captured):
            print(f"\n[{idx}] {entry['member']}  signature=({entry['signature']})")
            for i, a in enumerate(entry["args"]):
                print(f"    arg[{i}]:")
                for line in describe(a, 2):
                    print(line)

    print(f"\nTotal signals captured: {len(captured)}")

    # ---- Specifically highlight lookup table + preedit -------------------- #
    print("\n\n" + "#" * 70)
    print("# FOCUS: UpdateLookupTable & UpdatePreeditText")
    print("#" * 70)
    for entry in captured:
        if entry["member"] in ("UpdateLookupTable",
                               "UpdatePreeditText",
                               "CommitText",
                               "UpdatePreeditTextWithMode"):
            print(f"\n----- {entry['member']} sig=({entry['signature']}) -----")
            for i, a in enumerate(entry["args"]):
                print(f"arg[{i}]:")
                for line in describe(a, 1):
                    print(line)

    try:
        bus.remove_message_filter(filt)
    except Exception:
        pass


if __name__ == "__main__":
    main()
