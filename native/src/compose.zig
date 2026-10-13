const std = @import("std");
const dtoa = @import("dtoa.zig");

pub const SlotType = enum(u32) {
    text = 0,
    number = 1,
    integer = 2,
};

pub const ComposeSlot = extern struct {
    index: u32,
    length: u32,
    slot_type: SlotType,
    text_ptr: ?[*]const u16,
    text_len: u32,
    number_val: f64,
    decimals: i32,
};

pub fn compose(
    template: []const u16,
    slots: []const ComposeSlot,
    out: []u16,
) usize {
    var out_idx: usize = 0;
    var last_tpl: usize = 0;

    for (slots) |slot| {
        const from = @min(slot.index, template.len);
        if (from > last_tpl) {
            const static_len = from - last_tpl;
            const copy_len = @min(static_len, out.len - out_idx);
            @memcpy(out[out_idx..out_idx + copy_len], template[last_tpl..last_tpl + copy_len]);
            out_idx += copy_len;
        }

        switch (slot.slot_type) {
            .text => {
                if (slot.text_ptr) |ptr| {
                    const text_len = slot.text_len;
                    const copy_len = @min(text_len, out.len - out_idx);
                    @memcpy(out[out_idx..out_idx + copy_len], ptr[0..copy_len]);
                    out_idx += copy_len;
                }
            },
            .number => {
                if (out_idx < out.len) {
                    const written = dtoa.formatDouble(slot.number_val, slot.decimals, out[out_idx..]);
                    out_idx += written;
                }
            },
            .integer => {
                if (out_idx < out.len) {
                    const int_val: i64 = @intFromFloat(slot.number_val);
                    const written = dtoa.formatInt(int_val, out[out_idx..]);
                    out_idx += written;
                }
            },
        }

        last_tpl = @min(slot.index + slot.length, template.len);
    }

    if (last_tpl < template.len and out_idx < out.len) {
        const static_len = template.len - last_tpl;
        const copy_len = @min(static_len, out.len - out_idx);
        @memcpy(out[out_idx..out_idx + copy_len], template[last_tpl..last_tpl + copy_len]);
        out_idx += copy_len;
    }

    return out_idx;
}
