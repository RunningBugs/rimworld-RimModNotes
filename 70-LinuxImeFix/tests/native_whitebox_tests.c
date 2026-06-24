#define _GNU_SOURCE
#include <assert.h>
#include <dlfcn.h>
#include <errno.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <sys/time.h>
#include <time.h>
#include <unistd.h>

#include "../1.6/Source/linux_ime_native.c"

static void die(const char *msg) {
    perror(msg);
    exit(1);
}

static void mkdir_p(const char *path) {
    char tmp[1024];
    snprintf(tmp, sizeof(tmp), "%s", path);
    for (char *p = tmp + 1; *p; p++) {
        if (*p == '/') {
            *p = '\0';
            if (mkdir(tmp, 0700) != 0 && errno != EEXIST) die("mkdir");
            *p = '/';
        }
    }
    if (mkdir(tmp, 0700) != 0 && errno != EEXIST) die("mkdir");
}

static void write_file(const char *path, const char *content, time_t mtime) {
    FILE *f = fopen(path, "w");
    if (!f) die("fopen");
    fputs(content, f);
    fclose(f);
    struct timeval times[2] = {{mtime, 0}, {mtime, 0}};
    if (utimes(path, times) != 0) die("utimes");
}

static void test_find_ibus_address_prefers_matching_display(void) {
    char template[] = "/tmp/linuximefix-test-home-XXXXXX";
    char *home = mkdtemp(template);
    if (!home) die("mkdtemp");

    char bus_dir[1024];
    snprintf(bus_dir, sizeof(bus_dir), "%s/.config/ibus/bus", home);
    mkdir_p(bus_dir);

    char wrong[1200], right[1200];
    snprintf(wrong, sizeof(wrong), "%s/test-unix-0", bus_dir);
    snprintf(right, sizeof(right), "%s/test-unix-1", bus_dir);
    write_file(wrong, "IBUS_ADDRESS=wrong-newer\n", 2000);
    write_file(right, "IBUS_ADDRESS=right-older\n", 1000);

    setenv("HOME", home, 1);
    unsetenv("WAYLAND_DISPLAY");
    unsetenv("IBUS_ADDRESS");
    setenv("DISPLAY", ":1", 1);
    char *addr = find_ibus_address();
    assert(addr != NULL);
    assert(strcmp(addr, "right-older") == 0);
    free(addr);
}

static void test_find_ibus_address_prefers_wayland_and_skips_dead_pid(void) {
    char template[] = "/tmp/linuximefix-test-home-XXXXXX";
    char *home = mkdtemp(template);
    if (!home) die("mkdtemp");

    char bus_dir[1024];
    snprintf(bus_dir, sizeof(bus_dir), "%s/.config/ibus/bus", home);
    mkdir_p(bus_dir);

    char stale_x11[1200], live_wayland[1200], fallback[1200];
    snprintf(stale_x11, sizeof(stale_x11), "%s/test-unix-0", bus_dir);
    snprintf(live_wayland, sizeof(live_wayland), "%s/test-unix-wayland-0", bus_dir);
    snprintf(fallback, sizeof(fallback), "%s/test-unix-1", bus_dir);

    write_file(stale_x11, "IBUS_ADDRESS=stale-x11\nIBUS_DAEMON_PID=99999999\n", 3000);
    char content[256];
    snprintf(content, sizeof(content), "IBUS_ADDRESS=live-wayland\nIBUS_DAEMON_PID=%ld\n", (long)getpid());
    write_file(live_wayland, content, 1000);
    write_file(fallback, "IBUS_ADDRESS=fallback\n", 2000);

    setenv("HOME", home, 1);
    setenv("DISPLAY", ":0", 1);
    setenv("WAYLAND_DISPLAY", "wayland-0", 1);
    unsetenv("IBUS_ADDRESS");
    char *addr = find_ibus_address();
    assert(addr != NULL);
    assert(strcmp(addr, "live-wayland") == 0);
    free(addr);

    setenv("IBUS_ADDRESS", "from-env", 1);
    addr = find_ibus_address();
    assert(addr != NULL);
    assert(strcmp(addr, "from-env") == 0);
    free(addr);
}

static void test_utf8_safe_prefix_len(void) {
    const char *s = "你a🙂"; /* 3 + 1 + 4 bytes */
    assert(utf8_safe_prefix_len(s, 0) == 0);
    assert(utf8_safe_prefix_len(s, 1) == 0);
    assert(utf8_safe_prefix_len(s, 2) == 0);
    assert(utf8_safe_prefix_len(s, 3) == 3);
    assert(utf8_safe_prefix_len(s, 4) == 4);
    assert(utf8_safe_prefix_len(s, 7) == 4);
    assert(utf8_safe_prefix_len(s, 8) == 8);
}

static void test_public_getters_do_not_split_utf8(void) {
    char buf[16];

    strcpy(pending_commit, "你a");
    pending_commit_len = (int)strlen(pending_commit);
    assert(rimworld_ime_poll_utf8(buf, 3) == 0); /* 2 bytes cannot hold 你 */
    assert(pending_commit_len == 4);
    int n = rimworld_ime_poll_utf8(buf, 4);
    assert(n == 3);
    assert(strcmp(buf, "你") == 0);
    n = rimworld_ime_poll_utf8(buf, 4);
    assert(n == 1);
    assert(strcmp(buf, "a") == 0);

    strcpy(preedit_text, "你a");
    preedit_text_len = (int)strlen(preedit_text);
    preedit_visible = 1;
    assert(rimworld_ime_get_preedit(buf, 3) == 0);
    n = rimworld_ime_get_preedit(buf, 4);
    assert(n == 3);
    assert(strcmp(buf, "你") == 0);

    candidate_count = 1;
    strcpy(candidates[0], "你a");
    assert(rimworld_ime_get_candidate(0, buf, 3) == 0);
    n = rimworld_ime_get_candidate(0, buf, 4);
    assert(n == 3);
    assert(strcmp(buf, "你") == 0);
}

static void test_reset_clears_visible_state_without_connection(void) {
    preedit_visible = 1;
    lookup_visible = 1;
    candidate_count = 3;
    preedit_text_len = 3;
    strcpy(preedit_text, "abc");
    conn = NULL;
    ic_path = NULL;
    rimworld_ime_reset();
    assert(preedit_visible == 0);
    assert(lookup_visible == 0);
    assert(candidate_count == 0);
    assert(preedit_text_len == 0);
}

int main(void) {
    test_find_ibus_address_prefers_matching_display();
    test_find_ibus_address_prefers_wayland_and_skips_dead_pid();
    test_utf8_safe_prefix_len();
    test_public_getters_do_not_split_utf8();
    test_reset_clears_visible_state_without_connection();
    puts("native_whitebox_tests: PASS");
    return 0;
}
