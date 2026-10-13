const compose = @import("compose.zig");
const dtoa = @import("dtoa.zig");

export fn ov_compose_text(
    template_ptr: [*]const u16,
    template_len: usize,
    slots_ptr: [*]const compose.ComposeSlot,
    slots_count: usize,
    out_buf: [*]u16,
    out_max: usize,
) usize {
    return compose.compose(
        template_ptr[0..template_len],
        slots_ptr[0..slots_count],
        out_buf[0..out_max],
    );
}

export fn ov_format_double(val: f64, decimals: i32, out_buf: [*]u16, out_max: usize) usize {
    return dtoa.formatDouble(val, decimals, out_buf[0..out_max]);
}

export fn ov_format_int(val: i64, out_buf: [*]u16, out_max: usize) usize {
    return dtoa.formatInt(val, out_buf[0..out_max]);
}
