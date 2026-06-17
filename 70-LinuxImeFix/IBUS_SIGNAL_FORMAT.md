# IBus D-Bus Signal Wire Format (for C / libdbus-1 parsing)

Captured live via `test_signals.py` against a running `ibus-daemon` with the
Rime engine, typing "ni".  All structures below were observed on the wire and
are the exact layout you must parse in C.

## IBus session bus

- Address is read from `~/.config/ibus/bus/<machine-id>-unix-<display>`
  (newest file matching the X display number), variable `IBUS_ADDRESS=`.
  Example: `unix:path=/home/lisanhu/.cache/ibus/dbus-<rand>,guid=<guid>`
- Connect with `dbus_connection_open_private(addr, &err)`.

## Input context signals

Object path: `/org/freedesktop/IBus/InputContext_NNN`
Interface:   `org.freedesktop.IBus.InputContext`

| Signal                  | Signature | Args                                            |
|-------------------------|-----------|-------------------------------------------------|
| `CommitText`            | `v`       | text                                            |
| `UpdatePreeditText`     | `vub`     | text, cursor_pos (uint32), visible (bool)       |
| `UpdatePreeditTextWithMode` | `vubu` | text, cursor_pos, visible, mode (uint32)       |
| `UpdateAuxiliaryText`   | `vb`      | text, visible                                   |
| `UpdateLookupTable`     | `vb`      | table, visible                                  |
| `ShowPreeditText`       | (none)    |                                                 |
| `HidePreeditText`       | (none)    |                                                 |
| `ShowLookupTable`       | (none)    |                                                 |
| `HideLookupTable`       | (none)    |                                                 |
| `RegisterProperties`    | `v`       | prop_list                                       |
| `UpdateProperty`        | `v`       | prop                                            |

The first argument of every text/table signal is a **D-Bus variant `v`**
whose contained value is a **struct** (IBus serializable object).

## IBusText struct  (contained in the variant)

D-Bus signature of the struct content:
```
( s a{sv} s (s a{sv} av) )
```
Index | Type      | Meaning
------|-----------|------------------------------------------------
[0]   | string    | class name, always `"IBusText"`
[1]   | dict<sv>  | attributes (always empty in practice)
[2]   | string    | **the actual text** (this is what you want)
[3]   | struct    | `IBusAttrList`  = `( "IBusAttrList", a{sv}, av )`
        [3][2] = array of variant, each holding an `IBusAttribute`:
                 `( "IBusAttribute", a{sv}, u type, u value, u start, u end )`

So to get the preedit string: open variant -> struct -> read field [2] (string).

## IBusLookupTable struct  (contained in the variant)

D-Bus signature of the struct content:
```
( s a{sv} u u b b i av av )
```
Index | Type      | Meaning
------|-----------|------------------------------------------------
[0]   | string    | `"IBusLookupTable"`
[1]   | dict<sv>  | attributes (empty)
[2]   | uint32    | `page_size`        (e.g. 5)
[3]   | uint32    | `cursor_pos`       (e.g. 0)
[4]   | bool      | `cursor_visible`
[5]   | bool      | `round` (round-robin paging)
[6]   | int32     | `orientation`      (1 = vertical, 0 = horizontal)
[7]   | array<v>  | **candidates** — each variant holds an IBusText struct
[8]   | array<v>  | **labels**    — each variant holds an IBusText struct
                    (e.g. "1", "2", "3", ...)

### Extracting candidate strings (the key task)

For `UpdateLookupTable` (signature `vb`):

1. `arg0` is a variant. Open it -> a struct of 9 fields (above).
2. Field [7] is `av` (array of variants) = candidates.
3. For each element: it is a variant; open it -> IBusText struct;
   read field [2] (string) = the candidate text (e.g. `"你 wq wqi wqiy"`).

In libdbus-1 terms:
```
DBusMessageIter args, v_iter, struct_iter, field7_iter, cand_iter, elem_iter;
dbus_message_iter_init(msg, &args);            // signature "vb"
dbus_message_iter_recurse(&args, &v_iter);     // variant
dbus_message_iter_recurse(&v_iter, &struct_iter); // struct (9 fields)
// skip fields 0..6
for (int i = 0; i < 7; i++)
    dbus_message_iter_next(&struct_iter);
dbus_message_iter_recurse(&struct_iter, &field7_iter); // array of variants
while (dbus_message_iter_get_arg_type(&field7_iter) == DBUS_TYPE_VARIANT) {
    dbus_message_iter_recurse(&field7_iter, &elem_iter); // IBusText struct
    // skip [0]=name, [1]=attrs
    dbus_message_iter_next(&elem_iter);  // [1]
    dbus_message_iter_next(&elem_iter);  // [2]  <-- the string
    const char *text;
    dbus_message_iter_get_basic(&elem_iter, &text);  // candidate string
    dbus_message_iter_next(&field7_iter);
}
```
(Same pattern for the labels array at field [8].)

## Concrete captured example — typing "ni"

### UpdatePreeditText  (signature `vub`)
```
arg0 variant -> struct:
  [0] "IBusText"
  [1] {}                      (empty a{sv})
  [2] "ni"                    <-- preedit string
  [3] ("IBusAttrList", {}, [ ("IBusAttribute", {}, 1, 1, 0, 2) ])
arg1 uint32 = 2               (cursor_pos)
arg2 bool    = true           (visible)
```

### UpdateLookupTable  (signature `vb`)
```
arg0 variant -> struct:
  [0] "IBusLookupTable"
  [1] {}
  [2] u=5      (page_size)
  [3] u=0      (cursor_pos)
  [4] b=true   (cursor_visible)
  [5] b=false  (round)
  [6] i=1      (orientation = vertical)
  [7] av candidates (6 entries incl. trailing empty):
        v -> ("IBusText", {}, "悄",      (...))
        v -> ("IBusText", {}, "你 wq wqi wqiy", (... attr 2,0x606060,1,13 ...))
        v -> ("IBusText", {}, "拟 rny rnyw", (...))
        v -> ("IBusText", {}, "尼 nx nxv",  (...))
        v -> ("IBusText", {}, "呢 knx knxn", (...))
        v -> ("IBusText", {}, "",          (...))     <- trailing empty slot
  [8] av labels (5 entries): "1","2","3","4","5"
arg1 bool = true  (visible)
```

The candidate string in [7][i][2] is the full candidate line shown in the
lookup window (Han char + codes). For a pure-Han candidate the string is just
the character (e.g. `"悄"`); for code-assist candidates it is `"你 wq wqi wqiy"`.

## IBusAttribute struct  (inside IBusText attr list)

```
( "IBusAttribute", a{sv}, u type, u value, u start, u end )
```
- `type`:  1 = underline, 2 = foreground color, 3 = background color
- `value`: for type 2/3, an RGB int (e.g. 0x606060 = 6316128)
- `start`,`end`: byte offsets into the text

## CommitText

Same variant-of-IBusText layout as UpdatePreeditText arg0, but the signal has
**only one argument** (signature `v`): the committed text.  Field [2] of the
IBusText struct is the committed string.

---
Generated by `test_signals.py` (see this directory).
