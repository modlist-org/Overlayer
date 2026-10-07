using Newtonsoft.Json.Linq;
using Overlayer.Package;
using Xunit;

public sealed class O5cpTests {
    [Fact]
    public void Manifest_RoundTrip() {
        var manifest = new O5cpManifest {
            CanvasFile = "canvas.json",
            ThumbnailFile = "assets/thumbnail.png",
        };
        manifest.Package.Id = "MyCanvas_a3f9c1";
        manifest.Package.Name = "MyCanvas";
        manifest.Package.Author = "tester";
        manifest.Fonts.Add(new O5cpFontEntry {
            Key = "Gmarket",
            File = "resources/fonts/Gmarket.ttf",
            Sha256 = "ab",
            Bytes = 12,
        });
        manifest.Textures.Add(new O5cpTextureEntry {
            Key = "bg",
            File = "resources/textures/bg.png",
            Sha256 = "cd",
            Bytes = 34,
            MipChain = true,
            Folder = "pkg",
        });
        manifest.Sprites.Add(new O5cpSpriteEntry {
            Key = "bg_slice",
            TextureKey = "bg",
            Rect = [0f, 0f, 100f, 50f],
            Pivot = [0.5f, 0.5f],
            PixelsPerUnit = 100f,
            Border = [1f, 2f, 3f, 4f],
        });
        manifest.Scripts.Add(new O5cpScriptEntry {
            File = "scripts/fx.js",
            OriginalName = "fx.js",
            Sha256 = "ef",
            Bytes = 56,
        });
        manifest.ExcludedFonts.Add(new O5cpExcludedEntry { Key = "Missing", Sha256 = "00" });
        manifest.ExcludedScripts.Add(new O5cpExcludedEntry { Key = "skipped.js", Sha256 = "11" });

        string json = manifest.Serialize().ToString();
        Assert.True(O5cpManifest.TryParse(JToken.Parse(json), out var parsed, out string error), error);
        Assert.Equal("MyCanvas_a3f9c1", parsed.Package.Id);
        Assert.Equal("canvas.json", parsed.CanvasFile);
        Assert.Equal("assets/thumbnail.png", parsed.ThumbnailFile);
        Assert.Single(parsed.Fonts);
        Assert.Equal("bg", parsed.Textures[0].Key);
        Assert.True(parsed.Textures[0].MipChain);
        Assert.Equal("pkg", parsed.Textures[0].Folder);
        Assert.Equal(100f, parsed.Sprites[0].Rect[2]);
        Assert.Equal(4f, parsed.Sprites[0].Border[3]);
        Assert.Single(parsed.Scripts);
        Assert.Single(parsed.ExcludedFonts);
        Assert.Single(parsed.ExcludedScripts);
    }

    [Fact]
    public void Manifest_RejectsBadFormat() {
        Assert.False(O5cpManifest.TryParse(JToken.Parse(@"{""format"":""nope""}"), out _, out _));
        Assert.False(O5cpManifest.TryParse(JToken.Parse(@"{""format"":""o5cp""}"), out _, out _));
    }

    [Fact]
    public void Manifest_FutureVersion_LoadsBestEffort() {
        var token = JToken.Parse(@"{""format"":""o5cp"",""formatVersion"":99,""package"":{""id"":""x_000000""},""mystery"":{""a"":1}}");
        Assert.True(O5cpManifest.TryParse(token, out var parsed, out _));
        Assert.Equal("x_000000", parsed.Package.Id);
    }

    [Fact]
    public void Props_RoundTrip() {
        var props = new O5cpProps {
            ThumbnailPath = "{ModPath}/thumb.png",
            Author = "me",
            Description = "desc",
            Version = "2.0.0",
            License = "MIT",
            ExtraScripts = ["{ModPath}/JS/a.js", "{ModPath}/JS/b.js"],
        };
        var parsed = O5cpProps.Parse(props.Serialize());
        Assert.Equal(props.ThumbnailPath, parsed.ThumbnailPath);
        Assert.Equal("2.0.0", parsed.Version);
        Assert.Equal(2, parsed.ExtraScripts.Count);
    }

    [Fact]
    public void Props_ParseDefaults() {
        var parsed = O5cpProps.Parse(new JObject());
        Assert.Equal("1.0.0", parsed.Version);
        Assert.Empty(parsed.ExtraScripts);
        Assert.Equal("1.0.0", O5cpProps.Parse(null).Version);
    }

    [Fact]
    public void Scanner_StaticKeys() {
        var canvas = JToken.Parse(@"{
            ""Config"": { ""TextConfig"": { ""FontKey"": ""MyFont"" } },
            ""OvObjects"": [ { ""Config"": { ""ImageConfig"": { ""SpriteKey"": ""MySprite"" } } } ]
        }");
        var scan = O5cpKeyScanner.Scan(canvas);
        Assert.Contains("MyFont", scan.FontKeys);
        Assert.Contains("MySprite", scan.SpriteKeys);
        Assert.Empty(scan.GuessedFontKeys);
    }

    [Fact]
    public void Scanner_FxShape_StaticPlusGuessed() {
        var canvas = JToken.Parse(@"{
            ""Config"": {},
            ""OvObjects"": [ { ""Config"": {
                ""TextConfig"": { ""FontKey"": { ""Fx"": ""cond ? 'DynA' : 'DynB'"", ""Value"": ""StaticFont"" } },
                ""ImageConfig"": { ""SpriteKey"": { ""Fx"": ""x + 'DynSpr'"", ""Value"": null } }
            } } ]
        }");
        var scan = O5cpKeyScanner.Scan(canvas);
        Assert.Contains("StaticFont", scan.FontKeys);
        Assert.Contains("DynA", scan.GuessedFontKeys);
        Assert.Contains("DynB", scan.GuessedFontKeys);
        Assert.Contains("DynSpr", scan.GuessedSpriteKeys);
    }

    [Fact]
    public void Scanner_ExtractLiterals_DoubleQuotes() {
        var literals = O5cpKeyScanner.ExtractLiterals("a + \"Q1\" + 'Q2'");
        Assert.Contains("Q1", literals);
        Assert.Contains("Q2", literals);
    }

    [Fact]
    public void Mapper_RewritesStaticOnly() {
        var canvas = JToken.Parse(@"{
            ""Config"": { ""TextConfig"": {
                ""FontKey"": ""A"",
                ""Other"": { ""FontKey"": { ""Fx"": ""'A'"", ""Value"": ""B"" } }
            } },
            ""OvObjects"": [ { ""Config"": { ""ImageConfig"": { ""SpriteKey"": ""S"" } } } ]
        }");
        int count = O5cpKeyMapper.RewriteStaticKeys(canvas, key => "pkg/" + key);
        Assert.Equal(3, count);
        Assert.Equal("pkg/A", (string)canvas["Config"]["TextConfig"]["FontKey"]);
        Assert.Equal("pkg/B", (string)canvas["Config"]["TextConfig"]["Other"]["FontKey"]["Value"]);
        Assert.Equal("'A'", (string)canvas["Config"]["TextConfig"]["Other"]["FontKey"]["Fx"]);
        Assert.Equal("pkg/S", (string)canvas["OvObjects"][0]["Config"]["ImageConfig"]["SpriteKey"]);
    }

    [Fact]
    public void Package_WriteExtract_RoundTrip() {
        string dir = Path.Combine(Path.GetTempPath(), "o5cp_" + Guid.NewGuid().ToString("N"));
        string zip = Path.Combine(dir, "test.o5cp");
        try {
            O5cpPackage.WriteZip(zip, [
                ("manifest.json", System.Text.Encoding.UTF8.GetBytes(@"{""format"":""o5cp""}")),
                ("canvas.json", System.Text.Encoding.UTF8.GetBytes(@"{}")),
                ("resources/fonts/A.ttf", new byte[] { 1, 2, 3 }),
            ]);
            var written = O5cpPackage.ExtractZip(zip, Path.Combine(dir, "stage"));
            Assert.Equal(3, written.Count);
            Assert.True(File.Exists(Path.Combine(dir, "stage", "resources", "fonts", "A.ttf")));
        } finally {
            if(Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Package_RejectsUnsafePaths() {
        Assert.False(O5cpPackage.IsAllowedPath("../evil.json", out _));
        Assert.False(O5cpPackage.IsAllowedPath("resources/fonts/x.exe", out _));
        Assert.False(O5cpPackage.IsAllowedPath("Mod/evil.dll", out _));
        Assert.True(O5cpPackage.IsAllowedPath("manifest.json", out _));
        Assert.True(O5cpPackage.IsAllowedPath("scripts/a.js", out _));
        Assert.True(O5cpPackage.IsAllowedPath("assets/thumbnail.png", out _));
    }

    [Fact]
    public void Package_SanitizeAndId() {
        Assert.Equal("My_Font.ttf", O5cpPackage.SanitizeSegment("My Font", ".TTF"));
        Assert.Equal("res.bin", O5cpPackage.SanitizeSegment("...", ".bin"));
        string id = O5cpPackage.MakePackageId("My Canvas!", "a3f9c1ff");
        Assert.StartsWith("My_Canvas_", id);
        Assert.EndsWith("a3f9c1", id);
    }

    [Fact]
    public void TagScanner_FindsPlaceholders() {
        var canvas = JToken.Parse(@"{
            ""Config"": { ""TextConfig"": { ""Text"": ""{FullCombo} / {Accuracy:F1}"" } },
            ""OvObjects"": [ { ""Config"": {
                ""TextConfig"": { ""Text"": { ""Fx"": ""'{Kps}!' + 'x'"", ""Value"": ""{Miss}"" } },
                ""GraphConfig"": { ""JsCode"": ""50 + 40 * Math.sin(t * 2)"" }
            } } ]
        }");
        var names = O5cpTagScanner.Scan(canvas);
        Assert.Contains("FullCombo", names);
        Assert.Contains("Accuracy", names);
        Assert.Contains("Kps", names);
        Assert.Contains("Miss", names);
        Assert.DoesNotContain("t", names);
    }

    [Fact]
    public void Manifest_Modules_RoundTrip() {
        var manifest = new O5cpManifest();
        manifest.Package.Id = "M_000000";
        manifest.Modules.Add(new O5cpModuleEntry { Name = "ADOFAI", Version = "1.4.1", Author = "me" });
        Assert.True(O5cpManifest.TryParse(JToken.Parse(manifest.Serialize().ToString()), out var parsed, out _));
        var module = Assert.Single(parsed.Modules);
        Assert.Equal("ADOFAI", module.Name);
        Assert.Equal("1.4.1", module.Version);
    }

    [Fact]
    public void Mapper_RewritesKeyLiterals() {
        var canvas = JToken.Parse(@"{
            ""Config"": { ""TextConfig"": {
                ""FontKey"": { ""Fx"": ""c ? 'A' : 'Other'"", ""Value"": ""S"" },
                ""Text"": ""{A} and 'A'""
            } }
        }");
        var known = new HashSet<string> { "A" };
        int count = O5cpKeyMapper.RewriteKeyLiterals(canvas, lit => known.Contains(lit) ? "pkg/" + lit : null);
        Assert.Equal(1, count);
        Assert.Equal("c ? 'pkg/A' : 'Other'", (string)canvas["Config"]["TextConfig"]["FontKey"]["Fx"]);
        Assert.Equal("S", (string)canvas["Config"]["TextConfig"]["FontKey"]["Value"]);
        Assert.Equal("{A} and 'A'", (string)canvas["Config"]["TextConfig"]["Text"]);
    }

    [Fact]
    public void Format_PackageKeyAndScript() {
        Assert.True(O5cpFormat.IsPackageKey("pkg:abc123/bg"));
        Assert.False(O5cpFormat.IsPackageKey("bg"));
        Assert.False(O5cpFormat.IsPackageKey(null));
        Assert.True(O5cpFormat.IsPackageScript("pkg_a3f9c1f_fx.js"));
        Assert.True(O5cpFormat.IsPackageScript("/abs/path/pkg_A3F9C1F_fx.js"));
        Assert.False(O5cpFormat.IsPackageScript("my.js"));
        Assert.False(O5cpFormat.IsPackageScript("pkg_my.js"));
        Assert.False(O5cpFormat.IsPackageScript("pkg_zzzzzzz_fx.js"));
    }

    [Fact]
    public void Sha256_KnownVector() {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("abc");
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            O5cpPackage.ComputeSha256(data));
    }
}
