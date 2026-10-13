using Overlayer.Native;
using Xunit;

namespace Overlayer.Tests;

public sealed class CompositorTests {
    [Fact]
    public void Native_FormatDouble_MatchesExpectedPrecision() {
        Assert.True(OvCompositor.IsSupported);
        Span<char> buf = stackalloc char[32];
        int len = OvCompositor.FormatDouble(144.126, 2, buf);
        Assert.Equal("144.13", buf[..len].ToString());

        len = OvCompositor.FormatDouble(60.0, 1, buf);
        Assert.Equal("60.0", buf[..len].ToString());

        len = OvCompositor.FormatDouble(0.0, 0, buf);
        Assert.Equal("0", buf[..len].ToString());
    }

    [Fact]
    public void Native_Compose_CombinesStaticAndDynamicSegments() {
        Assert.True(OvCompositor.IsSupported);
        string template = "FPS: {Fps} | KPS: {Kps}";
        Span<char> fpsStr = stackalloc char[] { '1', '4', '4', '.', '0' };
        Span<char> kpsStr = stackalloc char[] { '1', '2', '.', '5' };

        Span<OvComposeSlot> slots = stackalloc OvComposeSlot[2];
        unsafe {
            fixed (char* pFps = fpsStr)
            fixed (char* pKps = kpsStr) {
                slots[0] = new OvComposeSlot {
                    Index = 5,
                    Length = 5, // "{Fps}"
                    SlotType = OvSlotType.Text,
                    TextPtr = pFps,
                    TextLen = (uint)fpsStr.Length,
                };
                slots[1] = new OvComposeSlot {
                    Index = 18,
                    Length = 5, // "{Kps}"
                    SlotType = OvSlotType.Text,
                    TextPtr = pKps,
                    TextLen = (uint)kpsStr.Length,
                };
            }
        }

        Span<char> outBuf = stackalloc char[64];
        int written = OvCompositor.Compose(template, slots, outBuf);
        Assert.Equal("FPS: 144.0 | KPS: 12.5", outBuf[..written].ToString());
    }

    [Fact]
    public void Native_Compose_DirectNumberFormattingInPlace() {
        Assert.True(OvCompositor.IsSupported);
        string template = "FPS: {Fps} | Score: {Score}";
        Span<OvComposeSlot> slots = stackalloc OvComposeSlot[2];
        slots[0] = new OvComposeSlot {
            Index = 5,
            Length = 5, // "{Fps}"
            SlotType = OvSlotType.Number,
            NumberVal = 143.99,
            Decimals = 2,
        };
        slots[1] = new OvComposeSlot {
            Index = 20,
            Length = 7, // "{Score}"
            SlotType = OvSlotType.Integer,
            NumberVal = 99999,
            Decimals = 0,
        };

        Span<char> outBuf = stackalloc char[64];
        int written = OvCompositor.Compose(template, slots, outBuf);
        Assert.Equal("FPS: 143.99 | Score: 99999", outBuf[..written].ToString());
    }
}
