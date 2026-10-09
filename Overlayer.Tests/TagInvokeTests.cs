using Overlayer.Tag.Core;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class TagInvokeTests {
    private static class FakeTags {
        public static int IntOfInt(int x) => x * 2;
        public static double DoubleOfDouble(double x) => x * 2d;
        public static string NameOfString(string s) => "hi " + s;
        public static int OptSecond(string a, int b = 7) => b;
        public static int SumAll(params int[] xs) {
            int total = 0;
            foreach (int x in xs) total += x;
            return total;
        }
        public static DayOfWeek EnumEcho(DayOfWeek d) => d;
        public static bool BoolEcho(bool b) => b;
    }

    private static TagCore TagOf(string method) => new(
        method,
        typeof(FakeTags).GetMethod(method, BindingFlags.Public | BindingFlags.Static),
        TagType.None,
        null);

    [Fact]
    public void Double_To_Int_Coerces_LikeTextEngine() {
        // Fx/JS calls hand over doubles (e.g. Tag.Fps(500)); this used to
        // throw InvalidCastException ("Specified cast is not valid") every frame.
        Assert.Equal(1000, TagOf(nameof(FakeTags.IntOfInt)).Invoke(500d));
    }

    [Fact]
    public void Exact_Types_Still_Pass_Through() {
        Assert.Equal(84, TagOf(nameof(FakeTags.IntOfInt)).Invoke(42));
        Assert.Equal("hi A", TagOf(nameof(FakeTags.NameOfString)).Invoke("A"));
        Assert.True(TagOf(nameof(FakeTags.BoolEcho)).Invoke(true) is true);
    }

    [Fact]
    public void String_To_Int_Parses() {
        Assert.Equal(400, TagOf(nameof(FakeTags.IntOfInt)).Invoke("200"));
    }

    [Fact]
    public void Int_To_Double_Widens() {
        Assert.Equal(6d, TagOf(nameof(FakeTags.DoubleOfDouble)).Invoke(3));
    }

    [Fact]
    public void Int_To_String_Stringifies() {
        Assert.Equal("hi 42", TagOf(nameof(FakeTags.NameOfString)).Invoke(42));
    }

    [Fact]
    public void Enum_From_String_Is_Case_Insensitive() {
        Assert.Equal(DayOfWeek.Friday, TagOf(nameof(FakeTags.EnumEcho)).Invoke("friday"));
    }

    [Fact]
    public void Enum_From_Int_Maps() {
        Assert.Equal(DayOfWeek.Monday, TagOf(nameof(FakeTags.EnumEcho)).Invoke(1));
    }

    [Fact]
    public void String_To_Bool_Parses() {
        Assert.Equal(true, TagOf(nameof(FakeTags.BoolEcho)).Invoke("true"));
    }

    [Fact]
    public void Missing_Optional_Arg_Fills_Default() {
        Assert.Equal(7, TagOf(nameof(FakeTags.OptSecond)).Invoke("x"));
    }

    [Fact]
    public void Missing_Required_Arg_Fills_Default_Of_Type() {
        Assert.Equal(0, TagOf(nameof(FakeTags.IntOfInt)).Invoke());
    }

    [Fact]
    public void Extra_Args_Are_Ignored() {
        Assert.Equal(10, TagOf(nameof(FakeTags.IntOfInt)).Invoke(5, "junk", 1.5d));
    }

    [Fact]
    public void Null_To_Value_Type_Gives_Default() {
        Assert.Equal(0, TagOf(nameof(FakeTags.IntOfInt)).Invoke(null));
    }

    [Fact]
    public void Params_Array_Packs_Extras() {
        Assert.Equal(6, TagOf(nameof(FakeTags.SumAll)).Invoke(1, 2, 3));
        Assert.Equal(0, TagOf(nameof(FakeTags.SumAll)).Invoke());
    }

    [Fact]
    public void Double_Nonzero_To_Bool_Coerces() {
        Assert.Equal(true, TagOf(nameof(FakeTags.BoolEcho)).Invoke(1.5d));
    }

    [Fact]
    public void Genuinely_Incompatible_Args_Still_Throw() {
        // Diagnostics must be preserved: garbage in keeps warning instead of
        // silently producing a wrong value. Normalization throws before the
        // delegate runs, so the raw InvalidCastException surfaces (callers
        // like TagAccessHelper catch Exception and fall back as before).
        Assert.Throws<InvalidCastException>(
            () => TagOf(nameof(FakeTags.IntOfInt)).Invoke(new object()));
    }
}
