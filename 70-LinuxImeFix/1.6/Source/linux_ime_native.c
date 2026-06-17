#define _GNU_SOURCE
#include <dbus/dbus.h>
#include <ctype.h>
#include <dirent.h>
#include <errno.h>
#include <locale.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <time.h>
#include <unistd.h>

/* ═══════════════════════════════════════════════════
 * LinuxImeFix Native Helper v7 — IBus D-Bus (C)
 *
 * Single-threaded design: ProcessKeyEvent is synchronous.
 * After IBus consumes a key, we drain the connection for
 * CommitText signals on the same thread. No signal thread,
 * no locking, no deadlock.
 * ═══════════════════════════════════════════════════ */

static DBusConnection *conn = NULL;
static char *ic_path = NULL;

static char pending_commit[4096];
static int pending_commit_len = 0;

static int logged_ready = 0;
static int logged_commit = 0;
static int logged_key = 0;

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

    char best_path[768] = "";
    time_t best_mtime = 0;
    struct dirent *ent;

    while ((ent = readdir(d))) {
        if (ent->d_name[0] == '.') continue;
        char path[768];
        snprintf(path, sizeof(path), "%s/%s", bus_dir, ent->d_name);
        struct stat st;
        if (stat(path, &st) != 0) continue;

        char suffix[32];
        snprintf(suffix, sizeof(suffix), "-unix-%d", disp_num);
        int matches = strstr(ent->d_name, suffix) != NULL;

        if (matches && st.st_mtime > best_mtime) {
            strcpy(best_path, path);
            best_mtime = st.st_mtime;
        } else if (best_path[0] == '\0' && st.st_mtime > best_mtime) {
            strcpy(best_path, path);
            best_mtime = st.st_mtime;
        }
    }
    closedir(d);
    if (best_path[0] == '\0') return NULL;

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

/* ── Drain CommitText signals from the connection ──
 *
 * Call this after ProcessKeyEvent returns true.
 * IBus sends CommitText as a signal on the same connection.
 * We read it synchronously with a short timeout.
 */
static void drain_commits(int timeout_ms) {
    if (!conn) return;

    /* Give IBus time to send the signal */
    if (!dbus_connection_read_write(conn, timeout_ms))
        return;

    DBusMessage *msg;
    while ((msg = dbus_connection_pop_message(conn)) != NULL) {
        if (dbus_message_is_signal(msg, "org.freedesktop.IBus.InputContext", "CommitText")) {
            /* CommitText arg: variant containing struct (sa{sv}sv)
               The last 's' in the struct is the text. */
            DBusMessageIter iter;
            if (dbus_message_iter_init(msg, &iter) &&
                dbus_message_iter_get_arg_type(&iter) == DBUS_TYPE_VARIANT) {
                DBusMessageIter var_iter;
                dbus_message_iter_recurse(&iter, &var_iter);

                if (dbus_message_iter_get_arg_type(&var_iter) == DBUS_TYPE_STRUCT) {
                    DBusMessageIter struct_iter;
                    dbus_message_iter_recurse(&var_iter, &struct_iter);

                    /* Skip type name (string) */
                    while (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_STRING)
                        dbus_message_iter_next(&struct_iter);
                    /* Skip properties (array) */
                    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_ARRAY)
                        dbus_message_iter_next(&struct_iter);

                    /* Now should be the text string */
                    if (dbus_message_iter_get_arg_type(&struct_iter) == DBUS_TYPE_STRING) {
                        const char *text = NULL;
                        dbus_message_iter_get_basic(&struct_iter, &text);
                        if (text && *text) {
                            int len = strlen(text);
                            int space = (int)sizeof(pending_commit) - 1 - pending_commit_len;
                            if (len > space) len = space;
                            if (len > 0) {
                                memcpy(pending_commit + pending_commit_len, text, len);
                                pending_commit_len += len;
                                pending_commit[pending_commit_len] = '\0';
                            }
                            fprintf(stderr, "[LinuxImeFix] commit: %s\n", text);
                            logged_commit = 1;
                        }
                    }
                }
            }
        }
        dbus_message_unref(msg);
    }
}

/* ── Init ── */

static int init_ibus(void) {
    if (conn) return 1;

    DBusError err;
    dbus_error_init(&err);

    char *addr = find_ibus_address();
    if (!addr) {
        fprintf(stderr, "[LinuxImeFix] Cannot find IBus address\n");
        return 0;
    }
    fprintf(stderr, "[LinuxImeFix] IBus address: %s\n", addr);

    conn = dbus_connection_open_private(addr, &err);
    free(addr);
    if (!conn || dbus_error_is_set(&err)) {
        fprintf(stderr, "[LinuxImeFix] Connect failed: %s\n", err.message);
        dbus_error_free(&err);
        return 0;
    }

    if (!dbus_bus_register(conn, &err)) {
        fprintf(stderr, "[LinuxImeFix] Register failed: %s\n", err.message);
        dbus_error_free(&err);
        return 0;
    }

    /* Create input context */
    DBusMessage *reply = dbus_call(conn,
        "org.freedesktop.IBus", "/org/freedesktop/IBus",
        "org.freedesktop.IBus", "CreateInputContext",
        DBUS_TYPE_STRING, "rimworld-ime-fix",
        DBUS_TYPE_INVALID);
    if (!reply) return 0;

    DBusMessageIter args;
    if (dbus_message_iter_init(reply, &args) &&
        dbus_message_iter_get_arg_type(&args) == DBUS_TYPE_OBJECT_PATH) {
        const char *path = NULL;
        dbus_message_iter_get_basic(&args, &path);
        if (path) ic_path = strdup(path);
    }
    dbus_message_unref(reply);
    if (!ic_path) { fprintf(stderr, "[LinuxImeFix] No IC path\n"); return 0; }
    fprintf(stderr, "[LinuxImeFix] IC path: %s\n", ic_path);

    /* Set capabilities */
    dbus_call(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "SetCapabilities",
        DBUS_TYPE_UINT32, (dbus_uint32_t)0xFFFFFFFF, DBUS_TYPE_INVALID);

    /* Focus in */
    dbus_call(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "FocusIn", DBUS_TYPE_INVALID);

    /* Add signal match */
    char match[512];
    snprintf(match, sizeof(match),
        "type='signal',interface='org.freedesktop.IBus.InputContext',member='CommitText',path='%s'",
        ic_path);
    dbus_bus_add_match(conn, match, &err);
    if (dbus_error_is_set(&err)) {
        fprintf(stderr, "[LinuxImeFix] Match add: %s\n", err.message);
        dbus_error_free(&err);
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

    if (!logged_key) {
        fprintf(stderr, "[LinuxImeFix] process_key(val=%d code=%d state=%d) => %d\n",
                keyval, keycode, state, consumed);
        logged_key = 1;
    }

    /* If consumed, drain for CommitText signal (short timeout) */
    if (consumed) {
        drain_commits(10); /* 10ms max */
    }

    return consumed ? 1 : 0;
}

__attribute__((visibility("default")))
int rimworld_ime_poll_utf8(char *buf, int len) {
    if (!buf || len <= 0) return 0;
    /* Also try a non-blocking drain */
    if (conn) drain_commits(0);
    if (pending_commit_len <= 0) return 0;
    int n = pending_commit_len;
    if (n > len - 1) n = len - 1;
    memcpy(buf, pending_commit, n);
    buf[n] = '\0';
    int remain = pending_commit_len - n;
    if (remain > 0) memmove(pending_commit, pending_commit + n, remain);
    pending_commit_len = remain;
    pending_commit[pending_commit_len] = '\0';
    return n;
}

__attribute__((visibility("default")))
void rimworld_ime_focus_in(void) {
    if (!conn || !ic_path) return;
    dbus_call(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "FocusIn", DBUS_TYPE_INVALID);
}

__attribute__((visibility("default")))
void rimworld_ime_focus_out(void) {
    if (!conn || !ic_path) return;
    dbus_call(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "FocusOut", DBUS_TYPE_INVALID);
}

__attribute__((visibility("default")))
void rimworld_ime_set_cursor(int x, int y) {
    if (!conn || !ic_path) return;
    DBusMessage *msg = dbus_message_new_method_call(
        "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "SetCursorLocation");
    if (!msg) return;
    DBusMessageIter args;
    dbus_message_iter_init_append(msg, &args);
    dbus_int32_t x32 = x, y32 = y, w32 = 0, h32 = 0;
    dbus_message_iter_append_basic(&args, DBUS_TYPE_INT32, &x32);
    dbus_message_iter_append_basic(&args, DBUS_TYPE_INT32, &y32);
    dbus_message_iter_append_basic(&args, DBUS_TYPE_INT32, &w32);
    dbus_message_iter_append_basic(&args, DBUS_TYPE_INT32, &h32);
    DBusError err;
    dbus_error_init(&err);
    DBusMessage *r = dbus_connection_send_with_reply_and_block(conn, msg, 500, &err);
    if (r) dbus_message_unref(r);
    if (dbus_error_is_set(&err)) dbus_error_free(&err);
    dbus_message_unref(msg);
}

__attribute__((visibility("default")))
void rimworld_ime_reset(void) {
    if (!conn || !ic_path) return;
    dbus_call(conn, "org.freedesktop.IBus", ic_path,
        "org.freedesktop.IBus.InputContext", "Reset", DBUS_TYPE_INVALID);
}

__attribute__((visibility("default")))
int rimworld_ime_is_ready(void) { return (conn && ic_path) ? 1 : 0; }

__attribute__((constructor))
static void init(void) {
    setlocale(LC_ALL, "");
    if (!getenv("XMODIFIERS") || !*getenv("XMODIFIERS"))
        setenv("XMODIFIERS", "@im=ibus", 1);
    fprintf(stderr, "[LinuxImeFix] native helper loaded (C/dbus v7)\n");
}