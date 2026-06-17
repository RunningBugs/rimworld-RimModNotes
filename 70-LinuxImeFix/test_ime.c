/* Standalone test for libLinuxImeFixNative.so
 * Loads the .so, inits IBus, sends test keys, polls commits.
 * Compile: gcc -o test_ime test_ime.c -ldl
 * Run:     ./test_ime
 */
#define _GNU_SOURCE
#include <dlfcn.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

typedef int (*init_fn)(void);
typedef int (*process_key_fn)(int keyval, int keycode, int state);
typedef int (*poll_utf8_fn)(char *buf, int len);
typedef int (*is_ready_fn)(void);
typedef void (*focus_in_fn)(void);
typedef void (*focus_out_fn)(void);
typedef void (*set_cursor_fn)(int x, int y);
typedef void (*reset_fn)(void);

int main(void) {
    const char *so_path = "1.6/Assemblies/libLinuxImeFixNative.so";

    void *h = dlopen(so_path, 2 /* RTLD_NOW */);
    if (!h) {
        printf("FAIL dlopen: %s\n", dlerror());
        return 1;
    }
    printf("OK dlopen %s\n", so_path);

    init_fn       p_init        = (init_fn)dlsym(h, "rimworld_ime_init");
    process_key_fn p_process    = (process_key_fn)dlsym(h, "rimworld_ime_process_key");
    poll_utf8_fn  p_poll        = (poll_utf8_fn)dlsym(h, "rimworld_ime_poll_utf8");
    is_ready_fn   p_is_ready    = (is_ready_fn)dlsym(h, "rimworld_ime_is_ready");
    focus_in_fn   p_focus_in    = (focus_in_fn)dlsym(h, "rimworld_ime_focus_in");
    focus_out_fn  p_focus_out   = (focus_out_fn)dlsym(h, "rimworld_ime_focus_out");
    set_cursor_fn p_set_cursor  = (set_cursor_fn)dlsym(h, "rimworld_ime_set_cursor");
    reset_fn      p_reset       = (reset_fn)dlsym(h, "rimworld_ime_reset");

    if (!p_init || !p_process || !p_poll) {
        printf("FAIL missing exports: init=%p process=%p poll=%p\n",
               (void*)p_init, (void*)p_process, (void*)p_poll);
        return 1;
    }
    printf("OK all exports found\n");

    printf("--- init ---\n");
    int ok = p_init();
    printf("init() => %d\n", ok);
    if (!ok) {
        printf("FAIL init failed\n");
        return 1;
    }

    if (p_is_ready) {
        printf("is_ready() => %d\n", p_is_ready());
    }

    if (p_focus_in) p_focus_in();
    if (p_set_cursor) p_set_cursor(100, 200);

    /* Test 1: type 'n' — should be consumed by Rime */
    printf("\n--- Test: type 'n' ---\n");
    int consumed = p_process('n', 0, 0);
    printf("process_key('n') => consumed=%d\n", consumed);
    char buf[4096];
    int n = p_poll(buf, sizeof(buf));
    printf("poll_utf8 => n=%d text='%.*s'\n", n, n > 0 ? n : 0, buf);

    /* Test 2: type 'i' — should be consumed, preedit continues */
    printf("\n--- Test: type 'i' ---\n");
    consumed = p_process('i', 0, 0);
    printf("process_key('i') => consumed=%d\n", consumed);
    n = p_poll(buf, sizeof(buf));
    printf("poll_utf8 => n=%d text='%.*s'\n", n, n > 0 ? n : 0, buf);

    /* Test 3: type space — should trigger commit */
    printf("\n--- Test: type space ---\n");
    consumed = p_process(32, 0, 0); /* space */
    printf("process_key(space) => consumed=%d\n", consumed);

    /* Wait a bit for async commit signal */
    usleep(50000); /* 50ms */
    n = p_poll(buf, sizeof(buf));
    printf("poll_utf8 => n=%d text='%.*s'\n", n, n > 0 ? n : 0, buf);

    /* Test 4: type English 'a' — should NOT be consumed */
    printf("\n--- Test: type 'a' (should not be consumed) ---\n");
    consumed = p_process('a', 0, 0);
    printf("process_key('a') => consumed=%d\n", consumed);
    n = p_poll(buf, sizeof(buf));
    printf("poll_utf8 => n=%d\n", n);

    /* Test 5: backspace — should not be consumed */
    printf("\n--- Test: backspace (should not be consumed) ---\n");
    consumed = p_process(0xFF08, 0, 0); /* XK_BackSpace */
    printf("process_key(BackSpace) => consumed=%d\n", consumed);

    if (p_focus_out) p_focus_out();
    if (p_reset) p_reset();

    printf("\n--- Done ---\n");
    dlclose(h);
    return 0;
}