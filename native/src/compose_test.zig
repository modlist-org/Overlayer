const std = @import("std");
const compose = @import("compose.zig");
const dtoa = @import("dtoa.zig");

fn toUtf16(comptime str: []const u8) [str.len]u16 {
    var result: [str.len]u16 = undefined;
    for (str, 0..) |c, i| {
        result[i] = c;
    }
    return result;
}

test "dtoa formatDouble fixed precision" {
    var out: [64]u16 = undefined;
    const len = dtoa.formatDouble(144.126, 2, &out);
    try std.testing.expectEqual(@as(usize, 6), len);
    const expected = toUtf16("144.13");
    try std.testing.expectEqualSlices(u16, &expected, out[0..len]);
}

test "dtoa formatInt" {
    var out: [64]u16 = undefined;
    const len = dtoa.formatInt(200, &out);
    try std.testing.expectEqual(@as(usize, 3), len);
    const expected = toUtf16("200");
    try std.testing.expectEqualSlices(u16, &expected, out[0..len]);
}

test "compose template with static text and dynamic number" {
    const tpl = toUtf16("FPS: {Fps:0.00} | Done");
    const num_val: f64 = 59.94;
    const slots = [_]compose.ComposeSlot{
        .{
            .index = 5,
            .length = 10, // {Fps:0.00}
            .slot_type = .number,
            .text_ptr = null,
            .text_len = 0,
            .number_val = num_val,
            .decimals = 2,
        },
    };

    var out: [128]u16 = undefined;
    const len = compose.compose(&tpl, &slots, &out);
    const expected = toUtf16("FPS: 59.94 | Done");
    try std.testing.expectEqualSlices(u16, &expected, out[0..len]);
}

test "compose template with text and integer" {
    const tpl = toUtf16("Hello {Name}, Score: {Score}!");
    const name = toUtf16("Alice");
    const slots = [_]compose.ComposeSlot{
        .{
            .index = 6,
            .length = 6, // {Name}
            .slot_type = .text,
            .text_ptr = &name,
            .text_len = name.len,
            .number_val = 0,
            .decimals = 0,
        },
        .{
            .index = 21,
            .length = 7, // {Score}
            .slot_type = .integer,
            .text_ptr = null,
            .text_len = 0,
            .number_val = 9999,
            .decimals = 0,
        },
    };

    var out: [128]u16 = undefined;
    const len = compose.compose(&tpl, &slots, &out);
    const expected = toUtf16("Hello Alice, Score: 9999!");
    try std.testing.expectEqualSlices(u16, &expected, out[0..len]);
}
