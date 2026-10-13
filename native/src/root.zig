const compose = @import("compose.zig");
const dtoa = @import("dtoa.zig");
const vertex = @import("vertex.zig");
const input = @import("input.zig");
const scanner = @import("scanner.zig");
const text = @import("text.zig");
const jsexpr = @import("jsexpr.zig");

export fn ov_js_compile(source: [*]const u16, len: usize, allow_t: bool) ?*jsexpr.Program {
    return jsexpr.compile(source[0..len], allow_t);
}

export fn ov_js_var_count(program: *const jsexpr.Program) u32 {
    return program.var_count;
}

export fn ov_js_eval(program: *const jsexpr.Program, t: f64, vars: [*]const f64, count: usize) f64 {
    return jsexpr.eval(program, t, vars[0..count]);
}

export fn ov_js_free(program: *jsexpr.Program) void {
    jsexpr.destroy(program);
}

export fn ov_tag_scan(source: [*]const u16, len: usize, output: [*]scanner.TagSpan, capacity: usize) usize {
    return scanner.scan(source[0..len], output[0..capacity]);
}

export fn ov_render_text(template: [*]const u16, len: usize, slots: [*]const text.Slot, count: usize, replacements: [*]const u16, replacement_len: usize, output: [*]u16, capacity: usize) usize {
    return text.render(template[0..len], slots[0..count], replacements[0..replacement_len], output[0..capacity]);
}

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

export fn ov_graph_build_quads(
    points_ptr: [*]const vertex.Point2D,
    count: usize,
    transform: vertex.MapTransform,
    half_thickness: f32,
    out_quads_ptr: [*]vertex.Quad2D,
    max_quads: usize,
) usize {
    return vertex.buildGraphQuads(
        points_ptr[0..count],
        transform,
        half_thickness,
        out_quads_ptr[0..max_quads],
    );
}

export fn ov_graph_build_fill_quads(
    points_ptr: [*]const vertex.Point2D,
    count: usize,
    transform: vertex.MapTransform,
    base_y: f32,
    out_quads_ptr: [*]vertex.Quad2D,
    max_quads: usize,
) usize {
    return vertex.buildGraphFillQuads(points_ptr[0..count], transform, base_y, out_quads_ptr[0..max_quads]);
}

export fn ov_raw_input_start() bool {
    return input.start();
}

export fn ov_raw_input_stop() void {
    input.stop();
}

export fn ov_raw_input_poll_count() u32 {
    return input.pollCount();
}

export fn ov_raw_input_is_running() bool {
    return input.isRunning();
}

export fn ov_raw_input_simulate_press() void {
    input.simulateKeyPress();
}
