using Overlayer.Native;
using Xunit;

namespace Overlayer.Tests;

public class RawInputTests {
    [Fact]
    public void Native_RawInput_Is_Supported() {
        Assert.True(OvRawInput.IsSupported);
    }

    [Fact]
    public void SimulateKeyPress_Increments_PollCount_And_Resets() {
        _ = OvRawInput.PollCount(); // Clear any existing

        OvRawInput.SimulateKeyPress();
        OvRawInput.SimulateKeyPress();
        OvRawInput.SimulateKeyPress();

        uint polled = OvRawInput.PollCount();
        Assert.Equal(3u, polled);

        // Next poll should be 0 because poll swaps with 0
        Assert.Equal(0u, OvRawInput.PollCount());
    }

    [Fact]
    public void Start_And_Stop_Cycle_Is_Safe() {
        bool started = OvRawInput.Start();
        if (started) {
            Assert.True(OvRawInput.IsRunning);
            OvRawInput.Stop();
            Assert.False(OvRawInput.IsRunning);
        }

        // Idempotent stop
        OvRawInput.Stop();
        Assert.False(OvRawInput.IsRunning);
    }
}
