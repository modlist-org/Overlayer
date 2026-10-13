const std = @import("std");

// Ryu algorithm
pub fn formatDouble(val: f64, decimals: i32, out: []u16) usize {
    if (out.len == 0) return 0;
    var ascii_buf: [128]u8 = undefined;
    const precision: ?usize = if (decimals >= 0) @intCast(decimals) else null;
    const s = std.fmt.float.render(&ascii_buf, val, .{
        .mode = .decimal,
        .precision = precision,
    }) catch return 0;

    const copy_len = @min(s.len, out.len);
    for (s[0..copy_len], 0..) |c, i| {
        out[i] = c;
    }
    return copy_len;
}

pub fn formatInt(val: i64, out: []u16) usize {
    if (out.len == 0) return 0;
    var ascii_buf: [64]u8 = undefined;
    const s = std.fmt.bufPrint(&ascii_buf, "{d}", .{val}) catch return 0;
    const copy_len = @min(s.len, out.len);
    for (s[0..copy_len], 0..) |c, i| {
        out[i] = c;
    }
    return copy_len;
}
