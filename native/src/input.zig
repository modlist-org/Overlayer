const std = @import("std");
const builtin = @import("builtin");

var running = std.atomic.Value(bool).init(false);
var press_count = std.atomic.Value(u32).init(0);
var bg_thread: ?std.Thread = null;

pub fn isRunning() bool {
    return running.load(.acquire);
}

pub fn pollCount() u32 {
    return press_count.swap(0, .acq_rel);
}

pub fn simulateKeyPress() void {
    _ = press_count.fetchAdd(1, .monotonic);
}

const windows_impl = if (builtin.os.tag == .windows) struct {
    const WH_KEYBOARD_LL = 13;
    const WM_KEYDOWN = 0x0100;
    const WM_SYSKEYDOWN = 0x0104;
    const WM_QUIT = 0x0012;

    const HOOKPROC = *const fn (c_int, usize, isize) callconv(.c) isize;
    const HHOOK = ?*anyopaque;
    const HINSTANCE = ?*anyopaque;

    extern "user32" fn SetWindowsHookExW(idHook: c_int, lpfn: HOOKPROC, hmod: HINSTANCE, dwThreadId: u32) callconv(.c) HHOOK;
    extern "user32" fn UnhookWindowsHookEx(hhk: HHOOK) callconv(.c) c_int;
    extern "user32" fn CallNextHookEx(hhk: HHOOK, nCode: c_int, wParam: usize, lParam: isize) callconv(.c) isize;
    extern "user32" fn GetMessageW(lpMsg: *anyopaque, hWnd: ?*anyopaque, wMsgFilterMin: u32, wMsgFilterMax: u32) callconv(.c) c_int;
    extern "user32" fn PostThreadMessageW(idThread: u32, msg: u32, wParam: usize, lParam: isize) callconv(.c) c_int;
    extern "kernel32" fn GetCurrentThreadId() callconv(.c) u32;

    var hook: HHOOK = null;
    var thread_id: u32 = 0;

    fn hookProc(nCode: c_int, wParam: usize, lParam: isize) callconv(.c) isize {
        if (nCode >= 0) {
            if (wParam == WM_KEYDOWN or wParam == WM_SYSKEYDOWN) {
                simulateKeyPress();
            }
        }
        return CallNextHookEx(hook, nCode, wParam, lParam);
    }

    fn run() void {
        thread_id = GetCurrentThreadId();
        hook = SetWindowsHookExW(WH_KEYBOARD_LL, hookProc, null, 0);
        if (hook == null) {
            running.store(false, .release);
            return;
        }

        var msg: [48]u8 = undefined;
        while (running.load(.acquire)) {
            if (GetMessageW(&msg, null, 0, 0) <= 0) break;
        }

        if (hook) |h| {
            _ = UnhookWindowsHookEx(h);
            hook = null;
        }
    }
} else struct {};

const linux_impl = if (builtin.os.tag == .linux) struct {
    const O_RDONLY = 0;
    const O_NONBLOCK = 0x800;
    const POLLIN = 0x0001;

    const pollfd = extern struct {
        fd: c_int,
        events: c_short,
        revents: c_short,
    };

    extern "c" fn open(path: [*:0]const u8, flags: c_int, ...) c_int;
    extern "c" fn close(fd: c_int) c_int;
    extern "c" fn read(fd: c_int, buf: [*]u8, count: usize) isize;
    extern "c" fn poll(fds: [*]pollfd, nfds: c_ulong, timeout: c_int) c_int;
    extern "c" fn usleep(usec: c_uint) c_int;

    fn run() void {
        var fds: [16]c_int = undefined;
        var fd_count: usize = 0;

        var dev_idx: usize = 0;
        while (dev_idx < 32 and fd_count < fds.len) : (dev_idx += 1) {
            var path_buf: [32]u8 = undefined;
            const s = std.fmt.bufPrint(&path_buf, "/dev/input/event{d}\x00", .{dev_idx}) catch continue;
            const fd = open(@ptrCast(s.ptr), O_RDONLY | O_NONBLOCK);
            if (fd >= 0) {
                fds[fd_count] = fd;
                fd_count += 1;
            }
        }

        if (fd_count == 0) {
            running.store(false, .release);
            return;
        }
        defer {
            for (fds[0..fd_count]) |fd| {
                _ = close(fd);
            }
        }

        var p_fds: [16]pollfd = undefined;
        for (0..fd_count) |i| {
            p_fds[i] = .{
                .fd = fds[i],
                .events = POLLIN,
                .revents = 0,
            };
        }

        const EV_KEY = 0x01;
        while (running.load(.acquire)) {
            const ready = poll(&p_fds, fd_count, 50);
            if (ready <= 0) continue;

            for (0..fd_count) |i| {
                if ((p_fds[i].revents & POLLIN) != 0) {
                    var ev: [24]u8 = undefined;
                    while (true) {
                        const bytes = read(fds[i], &ev, 24);
                        if (bytes < 24) break;
                        const ev_type = std.mem.readInt(u16, ev[16..18], builtin.cpu.arch.endian());
                        const ev_val = std.mem.readInt(i32, ev[20..24], builtin.cpu.arch.endian());
                        if (ev_type == EV_KEY and ev_val == 1) {
                            simulateKeyPress();
                        }
                    }
                }
            }
        }
    }
} else struct {};

pub fn start() bool {
    if (running.load(.acquire)) return true;

    running.store(true, .release);

    if (builtin.os.tag == .windows) {
        bg_thread = std.Thread.spawn(.{}, windows_impl.run, .{}) catch {
            running.store(false, .release);
            return false;
        };
        return true;
    } else if (builtin.os.tag == .linux) {
        bg_thread = std.Thread.spawn(.{}, linux_impl.run, .{}) catch {
            running.store(false, .release);
            return false;
        };
        _ = linux_impl.usleep(5000); // 5ms wait
        return running.load(.acquire);
    } else {
        running.store(false, .release);
        return false;
    }
}

pub fn stop() void {
    if (!running.load(.acquire)) return;
    running.store(false, .release);

    if (builtin.os.tag == .windows) {
        if (windows_impl.thread_id != 0) {
            _ = windows_impl.PostThreadMessageW(windows_impl.thread_id, windows_impl.WM_QUIT, 0, 0);
        }
    }

    if (bg_thread) |t| {
        t.join();
        bg_thread = null;
    }
}
