const std = @import("std");
const input = @import("input.zig");

test "input counter and polling" {
    _ = input.pollCount(); // Reset
    try std.testing.expectEqual(@as(u32, 0), input.pollCount());

    input.simulateKeyPress();
    input.simulateKeyPress();
    input.simulateKeyPress();

    try std.testing.expectEqual(@as(u32, 3), input.pollCount());
    // Subsequent poll should be 0 because swap resets it
    try std.testing.expectEqual(@as(u32, 0), input.pollCount());
}

test "input start and stop cycle" {
    const started = input.start();
    // It's ok if started is false on headless Linux without /dev/input permissions
    if (started) {
        try std.testing.expect(input.isRunning());
        input.stop();
        try std.testing.expect(!input.isRunning());
    }
}
