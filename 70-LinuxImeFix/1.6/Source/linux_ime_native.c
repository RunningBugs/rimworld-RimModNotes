#define _GNU_SOURCE
#include <dbus/dbus.h>
#include <ctype.h>
#include <dirent.h>
#include <errno.h>
#include <locale.h>
#include <pthread.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <time.h>
#include <unistd.h>

/* ═══════════════════════════════════════════════════
 * LinuxImeFix Native Helper v8 — IBus D-Bus (C)
 *
 * Adds preedit text + lookup table (candidate) support.
 * C# mod polls these to draw an in-game candidate window.
 * ═══════════════════════════════════════════════════ */

static DBusConnection *conn = NULL;
static char *ic_path = NULL;

/* Commit queue */
static char pending_commit[4096];
static int pending_commit_len = 0;

/* Preedit state */
static char preedit_text[1024];
static int preedit_text_len = 0;
static int preedit_cursor = 0;
static int preedit_visible = 0;

/* Lookup table state */
#define MAX_CANDIDATES 64
#define MAX_CANDIDATE_LEN 256
static char candidates[MAX_CANDIDATES][MAX_CANDIDATE_LEN];
static int candidate_count = 0;
static int lookup_cursor_pos = 0;
static int lookup_visible = 0;
static int lookup_orientation = 1; /* 1=vertical, 0=horizontal */

static int logged_ready = 0;

/* ── Find IBus address ── */

static char *find_ibus_address(void) {
    char bus_dir[512];
    const char *home = getenv("HOME");
    if (!home) home = "/tmp";
    snprintf(bus_dir, sizeof(bus_dir), "%s/.config/ibus/bus", home);

    DIR *d = opendir(bus_dir);
    if (!d) return NULL;

    const char *display = getenv("DISPLAY");
    int disp_num = 0;
    if (display && strchr(display, ':'))
        disp_num = atoi(strchr(display, ':') + 1);

    char best_match_path[768] = "";
    char best_fallback_path[768] = "";
    time_t best_match_mtime = 0;
    time_t best_fallback_mtime = 0;
    struct dirent *ent;

    char suffix[32];
    snprintf(suffix, sizeof(suffix), "-unix-%d", disp_num);

    while ((ent = readdir(d))) {
        if (ent->d_name[0] == '.') continue;
        char path[768];
        snprintf(path, sizeof(path), "%s/%s", bus_dir, ent->d_name);
        struct stat st;
        if (stat(path, &st) != 0) continue;

        int matches = strstr(ent->d_name, suffix) != NULL;
        if (matches && st.st_mtime >= best_match_mtime) {
            strcpy(best_match_path, path);
            best_match_mtime = st.st_mtime;
        }
        if (st.st_mtime >= best_fallback_mtime) {
            strcpy(best_fallback_path, path);
            best_fallback_mtime = st.st_mtime;
        }
    }
    closedir(d);

    const char *best_path = best_match_path[0] ? best_match_path : best_fallback_path;
    if (!best_path[0]) return NULL;

    FILE *f = fopen(best_path, "r");
    if (!f) return NULL;
    char *addr = NULL;
    char line[1024];
    while (fgets(line, sizeof(line), f)) {
        if (strncmp(line, "IBUS_ADDRESS=", 13) == 0) {
            char *val = line + 13;
            char *nl = strchr(val, '\n');
            if (nl) *nl = '\0';
            addr = strdup(val);
            break;
        }
    }
    fclose(f);
    return addr;
}

/* ── UTF-8 helpers ── */

static int utf8_safe_prefix_len(const char *s, int max_bytes) {
    if (!s || max_bytes <= 0) return 0;
    int i = 0;
    int last_good = 0;
    while (s[i] && i < max_bytes) {
        unsigned char c = (unsigned char)s[i];
        int need;
        if (c < 0x80) need = 1;
        else if ((c & 0xE0) == 0xC0) need = 2;
        else if ((c & 0xF0) == 0xE0) need = 3;
        else if ((c & 0xF8) == 0xF0) need = 4;
        else break;
        if (i + need > max_bytes) break;
        for (int j = 1; j < need; j++) {
            unsigned char cc = (unsigned char)s[i + j];
            if ((cc & 0xC0) != 0x80) return last_good;
        }
        i += need;
        last_good = i;
    }
    return last_good;
}

/* ── DBus call helper ── */

static DBusMessage *dbus_call(DBusConnection *c,
                               const char *dest, const char *path,
                               const char *iface, const char *method,
                               int first_arg_type, ...) {
    DBusMessage *msg = dbus_message_new_method_call(dest, path, iface, method);
    if (!msg) return NULL;
    DBusMessageIter args;
    dbus_message_iter_init_append(msg, &args);
    va_list ap;
    va_start(ap, first_arg_type);
    int type = first_arg_type;
    while (type != DBUS_TYPE_INVALID) {
        if (type == DBUS_TYPE_STRING) {
            const char *s = va_arg(ap, const char *);
            dbus_message_iter_append_basic(&args, DBUS_TYPE_STRING, &s);
        } else if (type == DBUS_TYPE_UINT32) {
            dbus_uint32_t v = va_arg(ap, dbus_uint32_t);
            dbus_message_iter_append_basic(&args, DBUS_TYPE_UINT32, &v);
        } else if (type == DBUS_TYPE_INT32) {
            dbus_int32_t v = va_arg(ap, dbus_int32_t);
            dbus_message_iter_append_basic(&args, DBUS_TYPE_INT32, &v);
        }
        type = va_arg(ap, int);
    }
    va_end(ap);
    DBusError err;
    dbus_error_init(&err);
    DBusMessage *reply = dbus_connection_send_with_reply_and_block(c, msg, 2000, &err);
    dbus_message_unref(msg);
    if (dbus_error_is_set(&err)) {
        fprintf(stderr, "[LinuxImeFix] %s.%s failed: %s\n", iface, method, err.message);
        dbus_error_free(&err);
    }
    return reply;
}

static void dbus_send_no_reply(DBusConnection *c,
                               const char *dest, const char *path,
                               const char *iface, const char *method,
                               int first_arg_type, ...) {
    DBusMessage *msg = dbus_message_new_method_call(dest, path, iface, method);
    if (!msg) return;

    DBusMessageIter args;
    dbus_message_iter_init_append(msg, &args);
    va_list ap;
    va_start(ap, first_arg_type);
    int type = first_arg_type;
    while (type != DBUS_TYPE_INVALID) {
        if (type == DBUS_TYPE_STRING) {
            const char *s = va_arg(ap, const char *);
            dbus_message_iter_append_basic(&args, DBUS_TYPE_STRING, &s);
        } else if (type == DBUS_TYPE_UINT32) {
            dbus_uint32_t v = va_arg(ap, dbus_uint32_t);
            dbus_message_iter_append_basic(&args, DBUS_TYPE_UINT32, &v);
        } else if (type == DBUS_TYPE_INT32) {
            dbus_int32_t v = va_arg(ap, dbus_int32_t);
            dbus_message_iter_append_basic(&args, DBUS_TYPE_INT32, &v);
        }
        type = va_arg(ap, int);
    }
    va_end(ap);

    dbus_message_set_no_reply(msg, TRUE);
    dbus_connection_send(c, msg, NULL);
    dbus_message_unref(msg);
}

/* ── Extract string from IBusText variant ──
 * variant -> struct(s a{sv} s ...) -> field[2] is the text
 */
static const char *extract_ibus_text(DBusMessageIter *variant_iter) {
    if (dbus_message_iter_get_arg_type(variant_iter) != DBUS_TYPE_VARIANT)
        return NULL;
    DBusMessageIter struct_iter;
    dbus_message_iter_recurse(variant_iter, &struct_iter);
    if (dbus_message_iter_get_arg_type(&struct_iter) != DBUS_TYPE_STRUCT)
        return NULL;
    dbus_message_iter_recurse(&struct_iter, &struct_iter);
    /* Skip [0]=name(string), [1]=attrs(dict) */
    dbus_message_iter_next(&struct_iter); /* skip name */
    dbus_message_iter_next(&struct_iter); /* skip attrs, now at [2]=text */
    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_STRING) {
        const char *text = NULL;
        dbus_message_iter_get_basic(&struct_iter, &text);
        return text;
    }
    return NULL;
}

/* ── Parse UpdateLookupTable signal ──
 * signature "vb": variant(struct IBusLookupTable), bool visible
 */
static void parse_lookup_table(DBusMessage *msg) {
    DBusMessageIter args;
    if (!dbus_message_iter_init(msg, &args)) return;

    /* arg0 = variant containing struct */
    if (dbus_message_iter_get_arg_type(&args) != DBUS_TYPE_VARIANT) return;
    DBusMessageIter var_iter;
    dbus_message_iter_recurse(&args, &var_iter);
    if (dbus_message_iter_get_arg_type(&var_iter) != DBUS_TYPE_STRUCT) return;

    DBusMessageIter struct_iter;
    dbus_message_iter_recurse(&var_iter, &struct_iter);

    /* Skip fields [0]=name, [1]=attrs */
    dbus_message_iter_next(&struct_iter);
    dbus_message_iter_next(&struct_iter);

    /* [2]=page_size(u), [3]=cursor_pos(u), [4]=cursor_visible(b), [5]=round(b), [6]=orientation(i) */
    dbus_uint32_t page_size = 0, cursor_pos = 0;
    dbus_bool_t cursor_visible = FALSE, round = FALSE;
    dbus_int32_t orientation = 1;

    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_UINT32) {
        dbus_message_iter_get_basic(&struct_iter, &page_size);
        dbus_message_iter_next(&struct_iter);
    }
    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_UINT32) {
        dbus_message_iter_get_basic(&struct_iter, &cursor_pos);
        dbus_message_iter_next(&struct_iter);
    }
    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_BOOLEAN) {
        dbus_message_iter_get_basic(&struct_iter, &cursor_visible);
        dbus_message_iter_next(&struct_iter);
    }
    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_BOOLEAN) {
        dbus_message_iter_get_basic(&struct_iter, &round);
        dbus_message_iter_next(&struct_iter);
    }
    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_INT32) {
        dbus_message_iter_get_basic(&struct_iter, &orientation);
        dbus_message_iter_next(&struct_iter);
    }

    /* [7] = av candidates */
    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_ARRAY) {
        DBusMessageIter cand_array;
        dbus_message_iter_recurse(&struct_iter, &cand_array);

        candidate_count = 0;
        while (dbus_message_iter_get_arg_type(&cand_array) == DBUS_TYPE_VARIANT &&
               candidate_count < MAX_CANDIDATES) {
            const char *text = extract_ibus_text(&cand_array);
            if (text && *text) {
                int len = utf8_safe_prefix_len(text, MAX_CANDIDATE_LEN - 1);
                memcpy(candidates[candidate_count], text, len);
                candidates[candidate_count][len] = '\0';
                candidate_count++;
            }
            dbus_message_iter_next(&cand_array);
        }
    }

    lookup_cursor_pos = cursor_pos;
    lookup_orientation = orientation;

    /* arg1 = bool visible */
    dbus_message_iter_next(&args);
    if (dbus_message_iter_get_arg_type(&args) == DBUS_TYPE_BOOLEAN) {
        dbus_message_iter_get_basic(&args, &lookup_visible);
    }

    fprintf(stderr, "[LinuxImeFix] lookup table: %d candidates, cursor=%d, visible=%d\n",
            candidate_count, lookup_cursor_pos, lookup_visible);
}

/* ── Parse UpdatePreeditText signal ──
 * signature "vub": variant(IBusText), uint32 cursor_pos, bool visible
 */
static void parse_preedit_text(DBusMessage *msg) {
    DBusMessageIter args;
    if (!dbus_message_iter_init(msg, &args)) return;

    if (dbus_message_iter_get_arg_type(&args) != DBUS_TYPE_VARIANT) return;
    const char *text = extract_ibus_text(&args);
    if (text) {
        preedit_text_len = utf8_safe_prefix_len(text, (int)sizeof(preedit_text) - 1);
        memcpy(preedit_text, text, preedit_text_len);
        preedit_text[preedit_text_len] = '\0';
    } else {
        preedit_text[0] = '\0';
        preedit_text_len = 0;
    }

    /* arg1 = cursor_pos */
    dbus_message_iter_next(&args);
    if (dbus_message_iter_get_arg_type(&args) == DBUS_TYPE_UINT32) {
        dbus_message_iter_get_basic(&args, &preedit_cursor);
    }

    /* arg2 = visible */
    dbus_message_iter_next(&args);
    if (dbus_message_iter_get_arg_type(&args) == DBUS_TYPE_BOOLEAN) {
        dbus_message_iter_get_basic(&args, &preedit_visible);
    }

    fprintf(stderr, "[LinuxImeFix] preedit: '%s' cursor=%d visible=%d\n",
            preedit_text, preedit_cursor, preedit_visible);
}

/* ── Drain all signals ── */

static void drain_signals(int timeout_ms) {
    if (!conn) return;
    if (!dbus_connection_read_write(conn, timeout_ms)) return;

    DBusMessage *msg;
    while ((msg = dbus_connection_pop_message(conn)) != NULL) {
        if (dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "CommitText")) {
            DBusMessageIter args;
            if (dbus_message_iter_init(msg, &args) &&
                dbus_message_iter_get_arg_type(&args) == DBUS_TYPE_VARIANT) {
                const char *text = extract_ibus_text(&args);
                if (text && *text) {
                    int space = (int)sizeof(pending_commit) - 1 - pending_commit_len;
                    int len = utf8_safe_prefix_len(text, space);
                    if (len > 0) {
                        memcpy(pending_commit + pending_commit_len, text, len);
                        pending_commit_len += len;
                        pending_commit[pending_commit_len] = '\0';
                    }
                    fprintf(stderr, "[LinuxImeFix] commit: %s\n", text);
                }
            }
        }
        else if (dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "UpdatePreeditText") ||
                 dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "UpdatePreeditTextWithMode")) {
            parse_preedit_text(msg);
        }
        else if (dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "UpdateLookupTable")) {
            parse_lookup_table(msg);
        }
        else if (dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "ShowPreeditText")) {
            preedit_visible = 1;
        }
        else if (dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "HidePreeditText")) {
            preedit_visible = 0;
            preedit_text[0] = '\0';
            preedit_text_len = 0;
        }
        else if (dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "ShowLookupTable")) {
            lookup_visible = 1;
        }
        else if (dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "HideLookupTable")) {
            lookup_visible = 0;
            candidate_count = 0;
        }

        dbus_message_unref(msg);
    }
}

static void cleanup_ibus(void) {
    if (ic_path) { free(ic_path); ic_path = NULL; }
    if (conn) {
        dbus_connection_close(conn);
        dbus_connection_unref(conn);
        conn = NULL;
    }
}

/* ── Init ── */

static int init_ibus(void) {
    if (conn && ic_path) return 1;

    DBusError err;
    dbus_error_init(&err);

    char *addr = find_ibus_address();
    if (!addr) { fprintf(stderr, "[LinuxImeFix] Cannot find IBus address\n"); return 0; }
    fprintf(stderr, "[LinuxImeFix] IBus address: %s\n", addr);

    conn = dbus_connection_open_private(addr, &err);
    free(addr);
    if (!conn || dbus_error_is_set(&err)) {
        fprintf(stderr, "[LinuxImeFix] Connect failed: %s\n", err.message ? err.message : "unknown");
        dbus_error_free(&err);
        cleanup_ibus();
        return 0;
    }

    if (!dbus_bus_register(conn, &err)) {
        fprintf(stderr, "[LinuxImeFix] Register failed: %s\n", err.message ? err.message : "unknown");
        dbus_error_free(&err);
        cleanup_ibus();
        return 0;
    }

    /* Create input context */
    DBusMessage *reply = dbus_call(conn,
        "org.freedesktop.IBus", "/org/freedesktop/IBus",
        "org.freedesktop.IBus", "CreateInputContext",
        DBUS_TYPE_STRING, "rimworld-ime-fix", DBUS_TYPE_INVALID);
    if (!reply) { cleanup_ibus(); return 0; }

    DBusMessageIter args;
    if (dbus_message_iter_init(reply, &args) &&
        dbus_message_iter_get_arg_type(&args) == DBUS_TYPE_OBJECT_PATH) {
        const char *path = NULL;
        dbus_message_iter_get_basic(&args, &path);
        if (path) ic_path = strdup(path);
    }
    dbus_message_unref(reply);
    if (!ic_path) { fprintf(stderr, "[LinuxImeFix] No IC path\n"); cleanup_ibus(); return 0; }
    fprintf(stderr, "[LinuxImeFix] IC path: %s\n", ic_path);

    /* Set capabilities */
    DBusMessage *cap_reply = dbus_call(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "SetCapabilities",
        DBUS_TYPE_UINT32, (dbus_uint32_t)0xFFFFFFFF, DBUS_TYPE_INVALID);
    if (cap_reply) dbus_message_unref(cap_reply);

    /* Focus in */
    dbus_send_no_reply(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "FocusIn", DBUS_TYPE_INVALID);

    /* Add signal matches for ALL signals we care about */
    const char *signals[] = {
        "CommitText", "UpdatePreeditText", "UpdatePreeditTextWithMode",
        "ShowPreeditText", "HidePreeditText",
        "UpdateLookupTable", "ShowLookupTable", "HideLookupTable",
        NULL
    };
    for (int i = 0; signals[i]; i++) {
        char match[512];
        snprintf(match, sizeof(match),
            "type='signal',interface='org.freedesktop.IBus.InputContext',member='%s',path='%s'",
            signals[i], ic_path);
        dbus_bus_add_match(conn, match, &err);
        if (dbus_error_is_set(&err)) {
            fprintf(stderr, "[LinuxImeFix] Match add %s: %s\n", signals[i], err.message);
            dbus_error_free(&err);
        }
    }

    fprintf(stderr, "[LinuxImeFix] IBus helper ready\n");
    logged_ready = 1;
    return 1;
}

/* ── Public API ── */

__attribute__((visibility("default")))
int rimworld_ime_init(void) { return init_ibus(); }

__attribute__((visibility("default")))
int rimworld_ime_process_key(int keyval, int keycode, int state) {
    if (!conn || !ic_path) return 0;

    DBusMessage *reply = dbus_call(conn,
        "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "ProcessKeyEvent",
        DBUS_TYPE_UINT32, (dbus_uint32_t)keyval,
        DBUS_TYPE_UINT32, (dbus_uint32_t)keycode,
        DBUS_TYPE_UINT32, (dbus_uint32_t)state,
        DBUS_TYPE_INVALID);
    if (!reply) return 0;

    dbus_bool_t consumed = FALSE;
    DBusMessageIter args;
    if (dbus_message_iter_init(reply, &args) &&
        dbus_message_iter_get_arg_type(&args) == DBUS_TYPE_BOOLEAN)
        dbus_message_iter_get_basic(&args, &consumed);
    dbus_message_unref(reply);

    if (consumed) {
        drain_signals(10);
    }
    return consumed ? 1 : 0;
}

__attribute__((visibility("default")))
int rimworld_ime_poll_utf8(char *buf, int len) {
    if (!buf || len <= 0) return 0;
    if (conn) drain_signals(0);
    if (pending_commit_len <= 0) return 0;
    int n = pending_commit_len;
    if (n > len - 1) n = utf8_safe_prefix_len(pending_commit, len - 1);
    if (n <= 0) return 0;
    memcpy(buf, pending_commit, n);
    buf[n] = '\0';
    int remain = pending_commit_len - n;
    if (remain > 0) memmove(pending_commit, pending_commit + n, remain);
    pending_commit_len = remain;
    pending_commit[pending_commit_len] = '\0';
    return n;
}

__attribute__((visibility("default")))
int rimworld_ime_get_preedit(char *buf, int len) {
    if (!buf || len <= 0) return 0;
    if (conn) drain_signals(0);
    if (preedit_text_len <= 0 || !preedit_visible) return 0;
    int n = preedit_text_len;
    if (n > len - 1) n = utf8_safe_prefix_len(preedit_text, len - 1);
    if (n <= 0) return 0;
    memcpy(buf, preedit_text, n);
    buf[n] = '\0';
    return n;
}

__attribute__((visibility("default")))
int rimworld_ime_get_preedit_cursor(void) {
    return preedit_cursor;
}

__attribute__((visibility("default")))
int rimworld_ime_is_preedit_visible(void) {
    if (conn) drain_signals(0);
    return preedit_visible ? 1 : 0;
}

__attribute__((visibility("default")))
int rimworld_ime_get_candidate_count(void) {
    if (conn) drain_signals(0);
    return candidate_count;
}

__attribute__((visibility("default")))
int rimworld_ime_get_candidate(int index, char *buf, int len) {
    if (!buf || len <= 0 || index < 0 || index >= candidate_count) return 0;
    int slen = strlen(candidates[index]);
    if (slen > len - 1) slen = utf8_safe_prefix_len(candidates[index], len - 1);
    if (slen <= 0) return 0;
    memcpy(buf, candidates[index], slen);
    buf[slen] = '\0';
    return slen;
}

__attribute__((visibility("default")))
int rimworld_ime_get_lookup_cursor(void) {
    return lookup_cursor_pos;
}

__attribute__((visibility("default")))
int rimworld_ime_is_lookup_visible(void) {
    if (conn) drain_signals(0);
    return lookup_visible ? 1 : 0;
}

__attribute__((visibility("default")))
void rimworld_ime_focus_in(void) {
    if (!conn || !ic_path) return;
    dbus_send_no_reply(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "FocusIn", DBUS_TYPE_INVALID);
}

__attribute__((visibility("default")))
void rimworld_ime_focus_out(void) {
    if (!conn || !ic_path) return;
    dbus_send_no_reply(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "FocusOut", DBUS_TYPE_INVALID);
}

__attribute__((visibility("default")))
void rimworld_ime_set_cursor(int x, int y) {
    if (!conn || !ic_path) return;
    dbus_int32_t x32 = x, y32 = y, w32 = 0, h32 = 0;
    dbus_send_no_reply(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "SetCursorLocation",
        DBUS_TYPE_INT32, x32, DBUS_TYPE_INT32, y32,
        DBUS_TYPE_INT32, w32, DBUS_TYPE_INT32, h32,
        DBUS_TYPE_INVALID);
}

__attribute__((visibility("default")))
void rimworld_ime_reset(void) {
    if (conn && ic_path) {
        dbus_send_no_reply(conn, "org.freedesktop.IBus", ic_path,
            "org.freedesktop.IBus.InputContext", "Reset", DBUS_TYPE_INVALID);
    }
    preedit_text[0] = '\0';
    preedit_text_len = 0;
    preedit_visible = 0;
    candidate_count = 0;
    lookup_visible = 0;
}

__attribute__((visibility("default")))
int rimworld_ime_is_ready(void) { return (conn && ic_path) ? 1 : 0; }

__attribute__((constructor))
static void init(void) {
    setlocale(LC_ALL, "");
    if (!getenv("XMODIFIERS") || !*getenv("XMODIFIERS"))
        setenv("XMODIFIERS", "@im=ibus", 1);
    fprintf(stderr, "[LinuxImeFix] native helper loaded (C/dbus v8)\n");
}