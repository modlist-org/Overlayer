using Overlayer.Tag.Core;
using UnityEngine.Device;

namespace Overlayer.TagImpl;

public static class Performance {

    [Tag(Desc = "Unscaled frame completion time in milliseconds")]
    public static double FrameTime => UnityEngine.Time.unscaledDeltaTime * 1000d;

    [Tag(Desc = "Current unscaled frames per second (FPS)")]
    public static double Fps => UnityEngine.Time.unscaledDeltaTime <= 0 ? 0 : 1d / UnityEngine.Time.unscaledDeltaTime;

    [Tag(Desc = "Total number of logical processor cores available on the system")]
    public static int ProcessorCount => SystemInfo.processorCount;
}