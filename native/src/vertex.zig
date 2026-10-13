const std = @import("std");

pub const Point2D = extern struct {
    x: f32,
    y: f32,
};

pub const Quad2D = extern struct {
    p0: Point2D,
    p1: Point2D,
    p2: Point2D,
    p3: Point2D,
};

pub const MapTransform = extern struct {
    scale_x: f32,
    offset_x: f32,
    scale_y: f32,
    offset_y: f32,
    x_min: f32,
};

pub fn buildGraphQuads(
    points: []const Point2D,
    transform: MapTransform,
    half_thickness: f32,
    out_quads: []Quad2D,
) usize {
    if (points.len < 2) return 0;
    var written: usize = 0;

    var i: usize = 0;
    while (i < points.len - 1 and written < out_quads.len) : (i += 1) {
        const raw_a = points[i];
        const raw_b = points[i + 1];

        var ax = raw_a.x * transform.scale_x + transform.offset_x;
        var ay = raw_a.y * transform.scale_y + transform.offset_y;
        const bx = raw_b.x * transform.scale_x + transform.offset_x;
        const by = raw_b.y * transform.scale_y + transform.offset_y;

        if (bx < transform.x_min) continue;

        if (ax < transform.x_min) {
            const dx = bx - ax;
            if (dx > 0.000001) {
                const t = (transform.x_min - ax) / dx;
                ax = transform.x_min;
                ay = ay + (by - ay) * t;
            } else {
                ax = bx;
                ay = by;
            }
        }

        const seg_dx = bx - ax;
        const seg_dy = by - ay;
        const sq_len = seg_dx * seg_dx + seg_dy * seg_dy;
        if (sq_len < 0.000001) continue;

        const inv_len = 1.0 / @sqrt(sq_len);
        const nx = -seg_dy * inv_len * half_thickness;
        const ny = seg_dx * inv_len * half_thickness;

        out_quads[written] = .{
            .p0 = .{ .x = ax - nx, .y = ay - ny },
            .p1 = .{ .x = ax + nx, .y = ay + ny },
            .p2 = .{ .x = bx + nx, .y = by + ny },
            .p3 = .{ .x = bx - nx, .y = by - ny },
        };
        written += 1;
    }

    return written;
}

// Build the area under the graph in one call, without any Unity objects or
// managed callbacks. Keep the preceding off-screen point to clip at x_min.
pub fn buildGraphFillQuads(
    points: []const Point2D,
    transform: MapTransform,
    base_y: f32,
    out_quads: []Quad2D,
) usize {
    if (points.len < 2) return 0;
    var written: usize = 0;
    var i: usize = 0;
    while (i < points.len - 1 and written < out_quads.len) : (i += 1) {
        var ax = points[i].x * transform.scale_x + transform.offset_x;
        var ay = points[i].y * transform.scale_y + transform.offset_y;
        const bx = points[i + 1].x * transform.scale_x + transform.offset_x;
        const by = points[i + 1].y * transform.scale_y + transform.offset_y;
        if (bx < transform.x_min) continue;
        if (ax < transform.x_min) {
            const dx = bx - ax;
            if (dx > 0.000001) {
                ay += (by - ay) * ((transform.x_min - ax) / dx);
                ax = transform.x_min;
            } else {
                ax = bx;
                ay = by;
            }
        }
        out_quads[written] = .{
            .p0 = .{ .x = ax, .y = base_y },
            .p1 = .{ .x = bx, .y = base_y },
            .p2 = .{ .x = bx, .y = by },
            .p3 = .{ .x = ax, .y = ay },
        };
        written += 1;
    }
    return written;
}
