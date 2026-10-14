const std = @import("std");
pub const TagSpan = extern struct {
    raw_start: u32,
    raw_len: u32,
    expr_start: u32,
    expr_len: u32,
    fmt_start: u32,
    fmt_len: u32,
};

fn skipString(input: []const u16, start: usize) usize {
    const quote = input[start];
    var i = start + 1;
    while (i < input.len) {
        if (input[i] == '\\') {
            i = @min(input.len, i + 2);
        } else if (input[i] == quote) {
            return i + 1;
        } else if (quote != '`' and input[i] == '\n') {
            return i;
        } else {
            i += 1;
        }
    }
    return input.len;
}

// UTF-16 offsets match .NET. Count all tags even when output is too short.
pub fn scan(input: []const u16, output: []TagSpan) usize {
    var count: usize = 0;
    var i: usize = 0;
    while (i < input.len) {
        if (input[i] != '{') {
            i += 1;
            continue;
        }
        if (i + 1 < input.len and input[i + 1] == '{') {
            i += 2;
            continue;
        }
        const start = i;
        var depth: usize = 1;
        var parens: usize = 0;
        var brackets: usize = 0;
        var colon: ?usize = null;
        // `?` of an open ternary: its `:` is not the format separator.
        var ternaries: usize = 0;
        var cursor = i + 1;
        var matched = false;
        while (cursor < input.len) {
            const c = input[cursor];
            if (c == '/' and cursor + 1 < input.len and input[cursor + 1] == '/') {
                cursor += 2;
                while (cursor < input.len and input[cursor] != '\n') : (cursor += 1) {}
                continue;
            }
            if (c == '/' and cursor + 1 < input.len and input[cursor + 1] == '*') {
                cursor += 2;
                while (cursor + 1 < input.len and !(input[cursor] == '*' and input[cursor + 1] == '/')) : (cursor += 1) {}
                cursor = @min(input.len, cursor + 2);
                continue;
            }
            if (c == '\'' or c == '"' or c == '`') {
                cursor = skipString(input, cursor);
                continue;
            }
            switch (c) {
                '(' => parens += 1,
                ')' => parens -|= 1,
                '[' => brackets += 1,
                ']' => brackets -|= 1,
                '{' => depth += 1,
                '}' => {
                    depth -= 1;
                    if (depth == 0) {
                        matched = true;
                        break;
                    }
                },
                '?' => if (depth == 1 and parens == 0 and brackets == 0 and colon == null) {
                    const next: u16 = if (cursor + 1 < input.len) input[cursor + 1] else 0;
                    if (next == '?') {
                        cursor += 1; // `??`
                    } else if (next != '.') { // `?.` is optional chaining
                        ternaries += 1;
                    }
                },
                ':' => if (depth == 1 and parens == 0 and brackets == 0 and colon == null) {
                    if (ternaries > 0) ternaries -= 1 else colon = cursor;
                },
                else => {},
            }
            cursor += 1;
        }
        if (matched) {
            if (count < output.len) output[count] = .{
                .raw_start = @intCast(start),
                .raw_len = @intCast(cursor - start + 1),
                .expr_start = @intCast(start + 1),
                .expr_len = @intCast((colon orelse cursor) - start - 1),
                .fmt_start = if (colon) |pos| @intCast(pos + 1) else 0,
                .fmt_len = if (colon) |pos| @intCast(cursor - pos - 1) else 0,
            };
            count += 1;
            i = cursor + 1;
        } else {
            i += 1;
        }
    }
    return count;
}

test "nested JS strings comments and formatting" {
    const text = std.unicode.utf8ToUtf16LeStringLiteral("{JSExpr: (() => { /* } */ return `{x}:${1}`; })()} {Score:F2}");
    var spans: [2]TagSpan = undefined;
    try std.testing.expectEqual(@as(usize, 2), scan(text, &spans));
    try std.testing.expectEqual(@as(u32, 6), spans[0].expr_len);
    try std.testing.expectEqual(@as(u32, 5), spans[1].expr_len);
    try std.testing.expectEqual(@as(u32, 2), spans[1].fmt_len);
}

test "ternary colons are not format separators" {
    const text = std.unicode.utf8ToUtf16LeStringLiteral("{a ? b : c} {a ? b : c:F2} {a?.b ?? c:F1} {JSExpr: x ? 1 : 2}");
    var spans: [4]TagSpan = undefined;
    try std.testing.expectEqual(@as(usize, 4), scan(text, &spans));
    try std.testing.expectEqual(@as(u32, 0), spans[0].fmt_len);
    try std.testing.expectEqual(@as(u32, 9), spans[1].expr_len);
    try std.testing.expectEqual(@as(u32, 2), spans[1].fmt_len);
    try std.testing.expectEqual(@as(u32, 9), spans[2].expr_len);
    try std.testing.expectEqual(@as(u32, 6), spans[3].expr_len);
}

test "capacity and UTF16 offsets" {
    const text = std.unicode.utf8ToUtf16LeStringLiteral("🎵{One}{Two}{{escaped}}");
    var spans: [1]TagSpan = undefined;
    try std.testing.expectEqual(@as(usize, 2), scan(text, &spans));
    try std.testing.expectEqual(@as(u32, 2), spans[0].raw_start);
    try std.testing.expectEqual(@as(usize, 2), scan(text, spans[0..0]));
}
