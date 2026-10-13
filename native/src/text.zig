const std = @import("std");

// Offset-based batch ABI; never retains a managed pointer.
pub const Slot = extern struct {
    index: u32,
    length: u32,
    offset: u32,
    text_length: u32,
};
pub const unchanged = std.math.maxInt(u32);

fn copy(source: []const u16, destination: []u16, cursor: *usize) void {
    const len = @min(source.len, destination.len - cursor.*);
    @memcpy(destination[cursor.*..][0..len], source[0..len]);
    cursor.* += len;
}

pub fn render(template: []const u16, slots: []const Slot, replacements: []const u16, output: []u16) usize {
    var written: usize = 0;
    var last: usize = 0;
    for (slots) |slot| {
        if (written == output.len) break;
        const from = @min(@as(usize, slot.index), template.len);
        const end = @min(@as(usize, slot.index) +| slot.length, template.len);
        if (from > last) copy(template[last..from], output, &written);
        if (slot.text_length == unchanged) {
            if (end > from) copy(template[from..end], output, &written);
        } else {
            const start = @min(@as(usize, slot.offset), replacements.len);
            const stop = @min(start +| slot.text_length, replacements.len);
            copy(replacements[start..stop], output, &written);
        }
        last = @max(last, end);
    }
    if (last < template.len and written < output.len) copy(template[last..], output, &written);
    return written;
}

test "null empty Unicode and truncation" {
    const template = std.unicode.utf8ToUtf16LeStringLiteral("A{One}B{Two}C{Three}D");
    const replacement = std.unicode.utf8ToUtf16LeStringLiteral("🎵");
    const slots = [_]Slot{
        .{ .index = 1, .length = 5, .offset = 0, .text_length = unchanged },
        .{ .index = 7, .length = 5, .offset = 0, .text_length = 0 },
        .{ .index = 13, .length = 7, .offset = 0, .text_length = 2 },
    };
    var output: [64]u16 = undefined;
    const expected = std.unicode.utf8ToUtf16LeStringLiteral("A{One}BC🎵D");
    const len = render(template, &slots, replacement, &output);
    try std.testing.expectEqualSlices(u16, expected, output[0..len]);
    try std.testing.expectEqual(@as(usize, 3), render(template, &slots, replacement, output[0..3]));
    try std.testing.expectEqualSlices(u16, expected[0..3], output[0..3]);
    try std.testing.expectEqual(@as(usize, 0), render(template, &slots, replacement, output[0..0]));
}
