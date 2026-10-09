using Overlayer.Update;
using System.IO.Compression;
using Xunit;

namespace Overlayer.Tests;

public sealed class UpdatePackageTests : IDisposable {
    private const string Asset = "Overlayer_ML_win.zip";
    private readonly string dir = Path.Combine(Path.GetTempPath(), "ovupdate-" + Guid.NewGuid().ToString("N"));

    public UpdatePackageTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, true);

    private static string Release(string tag, bool pre = false, bool draft = false, string host = "github.com") =>
        $$"""
        {"tag_name":"{{tag}}","prerelease":{{(pre ? "true" : "false")}},"draft":{{(draft ? "true" : "false")}},
         "html_url":"https://github.com/o/r/releases/{{tag}}",
         "assets":[{"name":"{{Asset}}","browser_download_url":"https://{{host}}/o/r/{{tag}}.zip",
                    "digest":"sha256:{{new string('a', 64)}}"}]}
        """;

    private static ReleaseInfo Pick(bool beta, params string[] releases) =>
        UpdatePackage.PickRelease("[" + string.Join(",", releases) + "]", new Version("5.8.1"), Asset, beta);

    [Fact]
    public void PicksNewestStableAndSkipsDraftsUntrustedAndOld() {
        ReleaseInfo best = Pick(false,
            Release("5.8.0"),
            Release("5.9.0"),
            Release("6.0.0", draft: true),
            Release("6.1.0", host: "evil.example"),
            Release("6.2.0-beta.1", pre: true));
        Assert.Equal("5.9.0", best.Tag);
        Assert.Equal(new string('a', 64), best.Sha256);
        Assert.Null(Pick(false, Release("5.8.1"), Release("5.0.0")));
    }

    [Fact]
    public void BetaChannelTakesPrereleaseButStableWinsTie() {
        Assert.Equal("6.2.0-beta.1", Pick(true, Release("5.9.0"), Release("6.2.0-beta.1", pre: true)).Tag);
        Assert.Equal("6.2.0", Pick(true, Release("6.2.0-beta.1", pre: true), Release("6.2.0")).Tag);
    }

    private string Zip(params (string Name, string Body)[] entries) {
        string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".zip");
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string name, string body) in entries) {
            using StreamWriter w = new(zip.CreateEntry(name).Open());
            w.Write(body);
        }
        return path;
    }

    [Fact]
    public void InstallReplacesBinariesAndLangButKeepsUserEdits() {
        string game = Path.Combine(dir, "game");
        Directory.CreateDirectory(Path.Combine(game, "Mods"));
        Directory.CreateDirectory(Path.Combine(game, "UserData/Overlayer/JS"));
        Directory.CreateDirectory(Path.Combine(game, "UserData/Overlayer/Lang"));
        File.WriteAllText(Path.Combine(game, "Mods/Overlayer.dll"), "old");
        File.WriteAllText(Path.Combine(game, "UserData/Overlayer/JS/Example.js"), "mine");
        File.WriteAllText(Path.Combine(game, "UserData/Overlayer/Lang/en-US.json"), "old");

        int count = UpdatePackage.Install(Zip(
            ("Mods/Overlayer.dll", "new"),
            ("UserLibs/O5Kit.dll", "new"),
            ("UserData/Overlayer/JS/Example.js", "new"),
            ("UserData/Overlayer/Lang/en-US.json", "new"),
            ("README.txt", "ignored")), Path.Combine(dir, "stage"), game);

        Assert.Equal(3, count);
        Assert.Equal("new", File.ReadAllText(Path.Combine(game, "Mods/Overlayer.dll")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(game, "UserLibs/O5Kit.dll")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(game, "UserData/Overlayer/Lang/en-US.json")));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(game, "UserData/Overlayer/JS/Example.js")));
        Assert.False(File.Exists(Path.Combine(game, "README.txt")));
        Assert.Empty(Directory.GetFiles(game, "*" + UpdatePackage.OldSuffix, SearchOption.AllDirectories));
    }

    [Fact]
    public void InstallRejectsZipSlipAndMissingPayloadBeforeTouchingGame() {
        string game = Path.Combine(dir, "game");
        Directory.CreateDirectory(Path.Combine(game, "Mods"));
        File.WriteAllText(Path.Combine(game, "Mods/Overlayer.dll"), "old");

        Assert.Throws<InvalidDataException>(() => UpdatePackage.Install(
            Zip(("Mods/Overlayer.dll", "new"), ("Mods/../../escape.dll", "x")), Path.Combine(dir, "stage"), game));
        Assert.Throws<InvalidDataException>(() => UpdatePackage.Install(
            Zip(("UserLibs/O5Kit.dll", "new")), Path.Combine(dir, "stage"), game));
        Assert.Equal("old", File.ReadAllText(Path.Combine(game, "Mods/Overlayer.dll")));
        Assert.False(File.Exists(Path.Combine(dir, "escape.dll")));
    }
}
