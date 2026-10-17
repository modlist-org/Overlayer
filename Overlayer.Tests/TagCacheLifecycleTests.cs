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
    [Fact]
    public void LocalTagsShadowGlobalsOnlyWhileCompilingTheirText() {
        using var v8 = new TestV8Host();
        const string name = "LocalScopeTag";
        TagManager.Set(new TagCore(name, (ScriptObject)v8.Engine.Evaluate("(() => 'global')"), [], TagType.None));
        var cache = new TagCache();
        var parsed = Parser.Parse("{" + name + "}")[0];

        var scope = new LocalTagScope();
        scope.Begin();
        const string src = "() => 'local'";
        Assert.False(scope.IsUnchanged(name, src));
        scope.Set(name, src, new TagCore(name, (ScriptObject)v8.Engine.Evaluate("(" + src + ")"), [], TagType.None));
        scope.End(true);
        var set = scope.Tags;
        Assert.NotNull(set);

        // Same registration next frame keeps the same set (no recompile).
        scope.Begin();
        Assert.True(scope.IsUnchanged(name, src));
        scope.End(true);
        Assert.Same(set, scope.Tags);

        TagCache.Locals = set;
        try {
            Assert.Equal("local", cache.GetOrCompile(parsed, out var localKey).Get());
            TagCache.Locals = null;
            Assert.Equal("global", cache.GetOrCompile(parsed, out var globalKey).Get());
            Assert.NotEqual(localKey, globalKey);
        } finally {
            TagCache.Locals = null;
        }

        // Removing the RegisterTag call drops the local tag.
        scope.Begin();
        scope.End(true);
        Assert.Null(scope.Tags);
    }
    public static string ReadTagByName(string tagName)
        => TagManager.TryGet(tagName, out var tag) ? tag.Invoke()?.ToString() : "missing";

    [Fact]
    public void TagReadingTagsSeeLocalTagsWhileTheirTextRenders() {
        using var v8 = new TestV8Host();
        TagManager.Set(new TagCore("ReadTagByName", typeof(TagCacheLifecycleTests).GetMethod(nameof(ReadTagByName)), TagType.None));
        var local = new LocalTagSet(new() {
            ["OnlyHere"] = new TagCore("OnlyHere", (ScriptObject)v8.Engine.Evaluate("(() => 42)"), [], TagType.None)
        });
        var cache = new TagCache();
        var parsed = Parser.Parse("{ReadTagByName(OnlyHere)}")[0];

        CompiledPlaceholder compiled;
        TagCache.Locals = local;
        try {
            compiled = cache.GetOrCompile(parsed);
        } finally {
            TagCache.Locals = null;
        }
        // Rendered later, outside the compile scope, like a real frame.
        Assert.Equal("42", compiled.Get());
        Assert.Null(TagCache.Locals);
        Assert.Equal("missing", cache.GetOrCompile(parsed).Get());
    }

    [Fact]
    public void TemplateRegisterTagParsesWholeAndRendersEmpty() {
        const string code = "RegisterTag(\"MaxXAcc\", () => {\n  let done = CurTile() - StartTile();\n  let left = LeftTile();\n"
            + "  let max = done + left > 0 ? (XAccuracy() * done + left) / (done + left) : 1;\n  return max * 100;\n"
            + "}, { Type: TagType.ProcessFormat, ReturnType: \"number\" })";
        var tags = Parser.Parse("{" + code + "}\nMax X-Acc: {MaxXAcc:0.00}%");
        Assert.Equal(2, tags.Count);
        Assert.Equal(code, tags[0].Expression);
        Assert.True(string.IsNullOrEmpty(tags[0].Format));
        Assert.True(LocalTagScope.IsRegistration(tags[0].Expression));
        Assert.Equal("", new TagCache().GetOrCompile(tags[0]).Get());
    }
}
