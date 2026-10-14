using Microsoft.ClearScript;
using Overlayer.Tag.Core;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;
using Xunit;

namespace Overlayer.Tests;

public sealed class TagCacheLifecycleTests {
    [Fact]
    public void ReloadRebindsJsWithoutDroppingDependenciesOfLiveOldGeneration() {
        using var v8 = new TestV8Host();
        const string name = "ReloadCompatibilityTag";
        var cache = new TagCache();
        TagManager.Set(new TagCore(name, (ScriptObject)v8.Engine.Evaluate("(() => 'old')"), [], TagType.None));
        var parsed = Parser.Parse("{" + name + "}")[0];
        var old = cache.Acquire(parsed, out var oldKey);
        Assert.Equal("old", old.Get());
        Assert.Contains(name, cache.GetActiveTagNames());

        cache.Clear();
        Assert.Contains(name, cache.GetActiveTagNames());
        TagManager.Set(new TagCore(name, (ScriptObject)v8.Engine.Evaluate("(() => 'new')"), [], TagType.None));
        var replacement = cache.Acquire(parsed, out var newKey);
        Assert.NotEqual(oldKey, newKey);
        Assert.Equal("new", replacement.Get());
        Assert.Equal("old", old.Get());

        cache.DecrementRef(oldKey);
        Assert.Contains(name, cache.GetActiveTagNames());
        cache.DecrementRef(newKey);
        Assert.Empty(cache.GetActiveTagNames());
    }

    [Fact]
    public void DuplicateBindingsRemainActiveUntilLastReferenceIsReleased() {
        using var v8 = new TestV8Host();
        const string name = "SharedCompatibilityTag";
        TagManager.Set(new TagCore(name, (ScriptObject)v8.Engine.Evaluate("(() => 42)"), [], TagType.None));
        var cache = new TagCache();
        var parsed = Parser.Parse("{" + name + "}")[0];
        cache.Acquire(parsed, out var key);
        cache.Acquire(parsed, out var secondKey);
        Assert.Equal(key, secondKey);
        cache.DecrementRef(key);
        Assert.Contains(name, cache.GetActiveTagNames());
        cache.DecrementRef(secondKey);
        Assert.Empty(cache.GetActiveTagNames());
    }

    [Fact]
    public void LegacyRetainApiCanRetainAnInvalidatedLiveBinding() {
        using var v8 = new TestV8Host();
        const string name = "RetiredCompatibilityTag";
        TagManager.Set(new TagCore(name, (ScriptObject)v8.Engine.Evaluate("(() => 42)"), [], TagType.None));
        var cache = new TagCache();
        cache.Acquire(Parser.Parse("{" + name + "()}")[0], out var key);
        cache.Clear();
        cache.IncrementRef(key);
        cache.DecrementRef(key);
        Assert.Contains(name, cache.GetActiveTagNames());
        cache.DecrementRef(key);
        Assert.Empty(cache.GetActiveTagNames());
    }

    public static double CallSyntaxSource() => 97.5;
    public static string CallSyntaxRange(string tagName, double minimum, double maximum, string minimumHex, string maximumHex, int maxLength = -1)
        => $"{tagName}|{minimum}|{maximum}|{minimumHex}|{maximumHex}|{maxLength}";

    [Fact]
    public void CallSyntaxPassesRawArgumentsToCSharpTags() {
        TagManager.Set(new TagCore("CallSyntaxSource", typeof(TagCacheLifecycleTests).GetMethod(nameof(CallSyntaxSource)), TagType.None));
        TagManager.Set(new TagCore("CallSyntaxRange", typeof(TagCacheLifecycleTests).GetMethod(nameof(CallSyntaxRange)), TagType.None));
        var cache = new TagCache();
        Assert.Equal("CallSyntaxSource|0|100|FFB2B2|FF0000|-1",
            cache.GetOrCompile(Parser.Parse("{CallSyntaxRange(CallSyntaxSource,0,100,FFB2B2,FF0000)}")[0]).Get());
        Assert.Equal("Combo|0.5|2|#fff|x|7",
            cache.GetOrCompile(Parser.Parse("{CallSyntaxRange('Combo', 0.5, 2, \"#fff\", 'x', 7)}")[0]).Get());
    }
}
