using Overlayer.Tag.Core;

public static class Time {
    [Tag(Desc = "Current system time in ticks (100-nanosecond intervals).")]
    public static long Ticks => DateTimeOffset.Now.Ticks;

    [Tag(Desc = "Current UTC time in ticks (100-nanosecond intervals).")]
    public static long UtcTicks => DateTimeOffset.Now.UtcTicks;

    [Tag(Desc = "Current millisecond (0-999).")]
    public static int Millisecond => DateTime.Now.Millisecond;

    [Tag(Desc = "Current second (0-59).")]
    public static int Second => DateTime.Now.Second;

    [Tag(Desc = "Current minute (0-59).")]
    public static int Minute => DateTime.Now.Minute;

    [Tag(Desc = "Current hour in 24-hour format (0-23).")]
    public static int Hour => DateTime.Now.Hour;

    [Tag(Desc = "Current hour in 12-hour format (1-12).")]
    public static int Hour12 => DateTime.Now.Hour % 12 == 0 ? 12 : DateTime.Now.Hour % 12;

    [Tag(Desc = "Current designator for morning or afternoon (AM or PM).")]
    public static string AmPm => DateTime.Now.ToString("tt", System.Globalization.CultureInfo.InvariantCulture);

    [Tag(Desc = "Returns true if the current system time is before noon (AM).")]
    public static bool IsAm => DateTime.Now.Hour < 12;

    [Tag(Desc = "Returns true if the current system time is afternoon or night (PM).")]
    public static bool IsPm => DateTime.Now.Hour >= 12;

    [Tag(Desc = "Current day of the month (1-31).")]
    public static int Day => DateTime.Now.Day;

    [Tag(Desc = "Current day of the year (1-366).")]
    public static int DayOfYear => DateTime.Now.DayOfYear;

    [Tag(Desc = "Current day of the week (e.g., Sunday).")]
    public static string DayOfWeek => DateTime.Now.DayOfWeek.ToString();

    [Tag(Desc = "Current month (1-12).")]
    public static int Month => DateTime.Now.Month;

    [Tag(Desc = "Current year (4 digits).")]
    public static int Year => DateTime.Now.Year;
}