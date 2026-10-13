const std = @import("std");

pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{ .preferred_optimize_mode = .ReleaseFast });
    const mod = b.createModule(.{
        .root_source_file = b.path("src/root.zig"),
        .target = target,
        .optimize = optimize,
        .link_libc = true,
    });
    const lib = b.addLibrary(.{
        .name = "overlayernative",
        .root_module = mod,
        .linkage = .dynamic,
    });
    b.installArtifact(lib);

    const test_step = b.step("test", "Run unit tests");

    const compose_test_mod = b.createModule(.{
        .root_source_file = b.path("src/compose_test.zig"),
        .target = target,
        .optimize = optimize,
        .link_libc = true,
    });
    const compose_tests = b.addTest(.{
        .name = "compose-test",
        .root_module = compose_test_mod,
    });
    const run_compose_tests = b.addRunArtifact(compose_tests);
    test_step.dependOn(&run_compose_tests.step);

    const vertex_test_mod = b.createModule(.{
        .root_source_file = b.path("src/vertex_test.zig"),
        .target = target,
        .optimize = optimize,
        .link_libc = true,
    });
    const vertex_tests = b.addTest(.{
        .name = "vertex-test",
        .root_module = vertex_test_mod,
    });
    const run_vertex_tests = b.addRunArtifact(vertex_tests);
    test_step.dependOn(&run_vertex_tests.step);

    const input_test_mod = b.createModule(.{
        .root_source_file = b.path("src/input_test.zig"),
        .target = target,
        .optimize = optimize,
        .link_libc = true,
    });
    const input_tests = b.addTest(.{
        .name = "input-test",
        .root_module = input_test_mod,
    });
    const run_input_tests = b.addRunArtifact(input_tests);
    test_step.dependOn(&run_input_tests.step);

    for ([_][]const u8{ "scanner", "text", "jsexpr" }) |name| {
        const tests = b.addTest(.{
            .name = name,
            .root_module = b.createModule(.{
                .root_source_file = b.path(b.fmt("src/{s}.zig", .{name})),
                .target = target,
                .optimize = optimize,
                .link_libc = true,
            }),
        });
        test_step.dependOn(&b.addRunArtifact(tests).step);
    }
}
