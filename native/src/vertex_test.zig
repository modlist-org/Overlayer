const std = @import("std");
const vertex = @import("vertex.zig");

test "buildGraphQuads horizontal segment" {
    const points = [_]vertex.Point2D{
        .{ .x = 0, .y = 0 },
        .{ .x = 10, .y = 0 },
    };
    const transform = vertex.MapTransform{
        .scale_x = 1.0,
        .offset_x = 0.0,
        .scale_y = 1.0,
        .offset_y = 0.0,
        .x_min = -100.0,
    };

    var out: [4]vertex.Quad2D = undefined;
    const count = vertex.buildGraphQuads(&points, transform, 2.0, &out);
    try std.testing.expectEqual(@as(usize, 1), count);

    // dx = 10, dy = 0, nx = 0, ny = 2.0
    // p0 = a - n = (0, -2)
    // p1 = a + n = (0, 2)
    // p2 = b + n = (10, 2)
    // p3 = b - n = (10, -2)
    try std.testing.expectApproxEqAbs(@as(f32, 0.0), out[0].p0.x, 0.001);
    try std.testing.expectApproxEqAbs(@as(f32, -2.0), out[0].p0.y, 0.001);
    try std.testing.expectApproxEqAbs(@as(f32, 0.0), out[0].p1.x, 0.001);
    try std.testing.expectApproxEqAbs(@as(f32, 2.0), out[0].p1.y, 0.001);
    try std.testing.expectApproxEqAbs(@as(f32, 10.0), out[0].p2.x, 0.001);
    try std.testing.expectApproxEqAbs(@as(f32, 2.0), out[0].p2.y, 0.001);
    try std.testing.expectApproxEqAbs(@as(f32, 10.0), out[0].p3.x, 0.001);
    try std.testing.expectApproxEqAbs(@as(f32, -2.0), out[0].p3.y, 0.001);
}

test "buildGraphQuads left clipping" {
    const points = [_]vertex.Point2D{
        .{ .x = -10, .y = 0 },
        .{ .x = 10, .y = 0 },
    };
    const transform = vertex.MapTransform{
        .scale_x = 1.0,
        .offset_x = 0.0,
        .scale_y = 1.0,
        .offset_y = 0.0,
        .x_min = 0.0,
    };

    var out: [4]vertex.Quad2D = undefined;
    const count = vertex.buildGraphQuads(&points, transform, 1.0, &out);
    try std.testing.expectEqual(@as(usize, 1), count);

    try std.testing.expectApproxEqAbs(@as(f32, 0.0), out[0].p0.x, 0.001);
    try std.testing.expectApproxEqAbs(@as(f32, 10.0), out[0].p2.x, 0.001);
}

test "fill clips preceding sample and preserves baseline" {
    const points = [_]vertex.Point2D{ .{ .x = -10, .y = 2 }, .{ .x = 10, .y = 6 } };
    const transform = vertex.MapTransform{ .scale_x = 1, .offset_x = 0, .scale_y = 1, .offset_y = 0, .x_min = 0 };
    var out: [1]vertex.Quad2D = undefined;
    try std.testing.expectEqual(@as(usize, 1), vertex.buildGraphFillQuads(&points, transform, -3, &out));
    try std.testing.expectEqual(@as(f32, 0), out[0].p0.x);
    try std.testing.expectEqual(@as(f32, -3), out[0].p0.y);
    try std.testing.expectEqual(@as(f32, 10), out[0].p2.x);
    try std.testing.expectEqual(@as(f32, 6), out[0].p2.y);
    try std.testing.expectEqual(@as(f32, 4), out[0].p3.y);
}

test "capacity limits emitted quads not scanned segments" {
    const points = [_]vertex.Point2D{
        .{ .x = -20, .y = 0 }, .{ .x = -10, .y = 0 },
        .{ .x = 0, .y = 0 },   .{ .x = 10, .y = 0 },
    };
    const transform = vertex.MapTransform{ .scale_x = 1, .offset_x = 0, .scale_y = 1, .offset_y = 0, .x_min = 0 };
    var out: [1]vertex.Quad2D = undefined;
    try std.testing.expectEqual(@as(usize, 1), vertex.buildGraphQuads(&points, transform, 1, &out));
    try std.testing.expectEqual(@as(f32, 10), out[0].p2.x);
    try std.testing.expectEqual(@as(usize, 1), vertex.buildGraphFillQuads(&points, transform, 0, &out));
    try std.testing.expectEqual(@as(usize, 0), vertex.buildGraphFillQuads(&points, transform, 0, out[0..0]));
    try std.testing.expectEqual(@as(usize, 0), vertex.buildGraphFillQuads(points[0..1], transform, 0, &out));
}
