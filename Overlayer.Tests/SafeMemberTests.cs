using Overlayer.Utility.Access;
using System.Reflection;
using Xunit;

namespace Overlayer.Tests;

public sealed class SafeMemberTests {
    private class DummyTarget {
        public static int Score = 7;
        public static string Name { get; set; } = "FullCombo";
        public static int Add(int a, int b) => a + b;
        public int Level;
    }

    private static SafeMember<T> Eager<T>(string member) {
        var m = new SafeMember<T>(new SafeMemberConfig(nameof(DummyTarget), member));
        SafeAccess.Init(typeof(DummyTarget).Assembly);
        return m;
    }

    [Fact]
    public void StaticField_ResolvesAndReads() {
        var m = Eager<int>(nameof(DummyTarget.Score));

        Assert.True(m.IsResolved);
        Assert.Equal(7, m.Get(null));
        Assert.True(m.TryGet(null, out int value));
        Assert.Equal(7, value);
    }

    [Fact]
    public void StaticProperty_ReadsAndWrites() {
        var m = Eager<string>(nameof(DummyTarget.Name));

        Assert.True(m.IsResolved);
        Assert.Equal("FullCombo", m.Get(null));
        Assert.True(m.TrySet(null, "Miss"));
        Assert.Equal("Miss", DummyTarget.Name);
        DummyTarget.Name = "FullCombo";
    }

    [Fact]
    public void StaticMethod_Invokes() {
        var m = Eager<int>(nameof(DummyTarget.Add));

        Assert.True(m.IsResolved);
        Assert.True(m.TryInvoke(null, out int result, 200, 174));
        Assert.Equal(374, result);
    }

    [Fact]
    public void InstanceField_UsesPassedInstance() {
        var m = Eager<int>(nameof(DummyTarget.Level));
        var target = new DummyTarget { Level = 5 };

        Assert.True(m.IsResolved);
        Assert.Equal(5, m.Get(target));
    }

    [Fact]
    public void MissingMember_ReturnsFallbackWithoutThrowing() {
        var m = Eager<int>("NoSuchMember_xyz");

        Assert.False(m.IsResolved);
        Assert.Equal(12345, m.Get(null, 12345));
        Assert.False(m.TryGet(null, out _));
    }

    [Fact]
    public void MissingType_ReturnsFallbackWithoutThrowing() {
        var m = new SafeMember<int>(new SafeMemberConfig("NoSuchType_xyz", "Whatever"));

        Assert.False(m.Resolve());
        Assert.False(m.IsResolved);
    }

    [Fact]
    public void LazyMode_ResolvesOnFirstUse() {
        var config = new SafeMemberConfig(nameof(DummyTarget), nameof(DummyTarget.Score)) {
            Mode = SafeResolveMode.Lazy,
        };
        var m = new SafeMember<int>(config);
        SafeAccess.Init(typeof(DummyTarget).Assembly);

        Assert.True(m.IsResolved);
        Assert.Equal(7, m.Get(null));
    }

    [Fact]
    public void ModeOverride_LazyDefersEagerMember() {
        var m = new SafeMember<int>(new SafeMemberConfig("NoSuchType_xyz", "Whatever"));
        SafeAccess.ModeOverride = SafeResolveMode.Lazy;
        try {
            Assert.False(m.IsResolved);
        } finally {
            SafeAccess.ModeOverride = null;
        }
    }

    [Fact]
    public void Access_GetterReadsDirectly() {
        var get = Access.Getter<int>(typeof(DummyTarget).FullName, nameof(DummyTarget.Score));

        Assert.Equal(7, get(null));
    }

    [Fact]
    public void Access_MissingMemberThrows() {
        Assert.Throws<MissingMemberException>(() =>
            Access.Getter<int>(typeof(DummyTarget).FullName, "NoSuchMember_xyz"));
    }

    [Fact]
    public void Access_MissingTypeThrows() {
        Assert.Throws<TypeLoadException>(() => Access.RequireType("NoSuchType_xyz"));
    }

    [Fact]
    public void Access_InvokerCallsWithConvertedArgs() {
        var invoke = Access.Invoker<int>(typeof(DummyTarget).FullName, nameof(DummyTarget.Add));

        Assert.Equal(374, invoke(null, [200, 174]));
    }
}
