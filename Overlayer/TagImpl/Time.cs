using Overlayer.Tag.Core;

namespace Overlayer.Impl;

public static class Time {
    [Tag]
    public static long Ticks => DateTimeOffset.Now.Ticks;

    [Tag]
    public static long UtcTicks => DateTimeOffset.Now.UtcTicks;

    [Tag]
    public static int Millisecond => DateTime.Now.Millisecond;

    [Tag]
    public static int Second => DateTime.Now.Second;

    [Tag]
    public static int Minute => DateTime.Now.Minute;

    [Tag]
    public static int Hour => DateTime.Now.Hour;

    [Tag]
    public static int Day => DateTime.Now.Day;

    [Tag]
    public static string DayOfWeek => DateTime.Now.DayOfWeek.ToString();

    [Tag]
    public static int Month => DateTime.Now.Month;

    [Tag]
    public static int Year => DateTime.Now.Year;
}