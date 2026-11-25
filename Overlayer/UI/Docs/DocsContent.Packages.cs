using Overlayer.Resource;

namespace Overlayer.UI.Docs;

internal static partial class DocsContent {
    private static DocChapter BuildPackagesAndModules() => new(
        "packages",
        UIIcon.Box,
        null,
        Tr("DOCS_PKG_TITLE", "Packages (.o5cp) & Modules"),
        Tr("DOCS_PKG_SUBTITLE", "Portable .o5cp canvas packages, built-in and file presets, and the C# OverlayerModule API"),
        DocSpecialKind.None,

        // 1. .o5cp Canvas Packages, O5cpProps & Safety
        new DocSection(
            Tr("DOCS_PKG_SEC_O5CP_TITLE", ".o5cp Canvas Packages, O5cpProps & Safety"),
            Tr("DOCS_PKG_SEC_O5CP_SUM", "Archive structure, strict extraction safety limits, and package metadata"),
            new DocParagraph(Tr(
                "DOCS_PKG_O5CP_P1",
                "An .o5cp file is a standard ZIP archive bundling a canvas layout with its fonts, textures, sprites, scripts, and thumbnail. To mitigate malformed or malicious archives, PackageStore enforces a strict path allowlist, rejects path traversal, and caps extraction at 256 MiB total and 2000 entries"
            )),
            new DocParagraph(Tr(
                "DOCS_PKG_O5CP_P2",
                "Package metadata is stored per canvas in O5cpProps and configured in the Canvas Props page prior to export. Bare/3D objects (stored under Canvases/Objects/*.json) are excluded from .o5cp exports"
            )),
            new DocTable(
                Tr("DOCS_PKG_O5CP_TBL_H1", "Allowlist / O5cpProps Field"),
                Tr("DOCS_PKG_O5CP_TBL_H2", "Details"),
                ("manifest.json / *.json", Tr("DOCS_PKG_O5CP_R1", "Package manifest and serialized canvas JSON")),
                ("resources/fonts/*.{ttf,otf}", Tr("DOCS_PKG_O5CP_R2", "Bundled font files")),
                ("resources/textures/*.{png,jpg,jpeg,bmp,tga}", Tr("DOCS_PKG_O5CP_R3", "Bundled texture files")),
                ("scripts/*.js", Tr("DOCS_PKG_O5CP_R4", "Bundled JavaScript files (referenced tags + extraScripts)")),
                ("assets/*.{png,jpg,jpeg}", Tr("DOCS_PKG_O5CP_R5", "Package thumbnail image")),
                ("O5cpProps", Tr("DOCS_PKG_O5CP_R6", "thumbnailPath, author, description, version, license, extraScripts[]"))
            )
        ),

        // 2. manifest.json Schema & Install / Promote Lifecycle
        new DocSection(
            Tr("DOCS_PKG_SEC_MANIFEST_TITLE", "manifest.json Schema & Install / Promote Lifecycle"),
            Tr("DOCS_PKG_SEC_MANIFEST_SUM", "Manifest asset descriptors, installed package namespacing, and Promotion into user space"),
            new DocParagraph(Tr(
                "DOCS_PKG_MAN_P1",
                "Installed packages reside under a 7-hex directory inside Packages/ alongside state.json. Bundled resources are namespaced with the package prefix to prevent collisions with user resources, and bundled scripts are written into JS/Script with a package hash prefix"
            )),
            new DocParagraph(Tr(
                "DOCS_PKG_MAN_P2",
                "Promoting a package copies its canvas into user Canvases (appending \" Copy\" to the canvas name) and resolves resource/script collisions via Reuse, Import, or Rename (default: Rename, appending \" (imported)\" to keys). When uninstalling a package, KeepScripts preserves its scripts as user_*.js files"
            )),
            new DocTable(
                Tr("DOCS_PKG_MAN_TBL_H1", "Artifact / Path"),
                Tr("DOCS_PKG_MAN_TBL_H2", "Convention"),
                ("Packages/<7-hex>/ + state.json", Tr("DOCS_PKG_MAN_R1", "Installed package directory and enabled/metadata state")),
                ("pkg:<id>/", Tr("DOCS_PKG_MAN_R2", "Resource key namespace for package fonts, textures, and sprites")),
                ("JS/Script/pkg_<hash>_*.js", Tr("DOCS_PKG_MAN_R3", "Installed package script filename; preserved as user_*.js via KeepScripts on removal")),
                ("\"<key> (imported)\" / \" Copy\"", Tr("DOCS_PKG_MAN_R4", "Default Rename key suffix and promoted canvas name suffix"))
            ),
            new DocCode(
                Tr("DOCS_PKG_MAN_CODE_CAP", "Example manifest.json (modules[] auto-detected by O5cpTagScanner)"),
                """
                {
                  "format": "o5cp",
                  "formatVersion": 1,
                  "package": {
                    "id": "a1b2c3d",
                    "name": "Clean HUD",
                    "version": "1.0.0",
                    "author": "Creator",
                    "description": "Minimal gameplay overlay",
                    "license": "MIT",
                    "createdAt": "2026-01-01T00:00:00Z",
                    "appVersion": "5.0.0",
                    "minAppVersion": "5.0.0"
                  },
                  "canvas": { "file": "canvas.json" },
                  "thumbnail": { "file": "assets/thumb.png" },
                  "resources": {
                    "fonts": [],
                    "textures": [],
                    "sprites": [],
                    "scripts": []
                  },
                  "modules": [],
                  "excluded": { "fonts": [], "textures": [], "scripts": [] },
                  "extensions": {}
                }
                """,
                DocsHighlight.Mode.Plain
            )
        ),

        // 3. Presets
        new DocSection(
            Tr("DOCS_PKG_SEC_PRESETS_TITLE", "Presets"),
            Tr("DOCS_PKG_SEC_PRESETS_SUM", "Built-in starter presets, user .o5cp files in Presets/, and module-registered presets"),
            new DocParagraph(Tr(
                "DOCS_PKG_PRESETS_P1",
                "Presets are reusable .o5cp templates accessible from the Overlayer tab and the HUD Editor star menu. Overlayer includes two built-in presets, loads .o5cp files placed in Presets/ automatically, and permits game modules to register custom presets via PresetStore.Register"
            )),
            new DocTable(
                Tr("DOCS_PKG_PRESETS_TBL_H1", "Source"),
                Tr("DOCS_PKG_PRESETS_TBL_H2", "ID / Location"),
                (Tr("DOCS_PKG_PRESETS_K1", "Built-in"), "overlayer:fps, overlayer:clock"),
                (Tr("DOCS_PKG_PRESETS_K2", "User Files"), "Presets/*.o5cp (ID: file:<name>)"),
                (Tr("DOCS_PKG_PRESETS_K3", "Game Modules"), Tr("DOCS_PKG_PRESETS_R3", "Registered at runtime via PresetStore.Register(preset)"))
            )
        ),

        // 4. Game Module API (OverlayerModule)
        new DocSection(
            Tr("DOCS_PKG_SEC_MOD_TITLE", "Game Module API (OverlayerModule)"),
            Tr("DOCS_PKG_SEC_MOD_SUM", "Extend Overlayer for a specific game using C# modules"),
            new DocParagraph(Tr(
                "DOCS_PKG_MOD_P1",
                "At startup, ModuleService scans already-loaded assemblies for concrete subclasses of OverlayerModule inside a namespace starting with Overlayer.Module (with a parameterless constructor). Override Name, Author, Version, and lifecycle hooks to register game-specific tags, presets, playback state, and fonts"
            )),
            new DocTable(
                Tr("DOCS_PKG_MOD_TBL_H1", "Hook / Extension Point"),
                Tr("DOCS_PKG_MOD_TBL_H2", "Purpose"),
                ("OnInitialize() / OnDispose()", Tr("DOCS_PKG_MOD_R1", "Setup and teardown hooks called when the module loads or unloads")),
                ("GetPreferredLanguage()", Tr("DOCS_PKG_MOD_R2", "Optional language code (e.g. \"ko-KR\") suggested on first run")),
                ("OnModEnabledChanged(bool enabled, bool isDispose)", Tr("DOCS_PKG_MOD_R3", "Notified when the Overlayer mod active state changes")),
                ("TagManager.RegisterAsync(Assembly)", Tr("DOCS_PKG_MOD_R4", "Registers static [Tag] methods from the module assembly")),
                ("PresetStore.Register / UnregisterByModule", Tr("DOCS_PKG_MOD_R5", "Registers or cleans up module-provided HUD presets")),
                ("PlaybackState.Register / RegisterPaused / RegisterAutoplay", Tr("DOCS_PKG_MOD_R6", "Provides playing, paused, and autoplay state; returns IDisposable")),
                ("TextFontProvider.Register(Func<TMP_FontAsset>)", Tr("DOCS_PKG_MOD_R7", "Supplies the game's default TMP font; returns IDisposable"))
            ),
            new DocCode(
                Tr("DOCS_PKG_MOD_CODE_CAP", "Minimal OverlayerModule skeleton"),
                """
                using System.Reflection;
                using Overlayer.ModuleAPI;
                using Overlayer.Package;
                using Overlayer.Tag.Core;

                namespace Overlayer.Module.MyGame;

                public sealed class MyGameModule : OverlayerModule {
                    private IDisposable _playing;
                    public override string Name => "MyGame";
                    public override string Author => "Author";
                    public override string Version => "1.0.0";

                    public override void OnInitialize() {
                        _ = TagManager.RegisterAsync(Assembly.GetExecutingAssembly());
                        _playing = PlaybackState.Register(() => GameState.IsPlaying);
                    }

                    public override string GetPreferredLanguage() => "ko-KR";

                    public override void OnDispose() {
                        _playing?.Dispose();
                        PresetStore.UnregisterByModule(Name);
                    }
                }
                """,
                DocsHighlight.Mode.Plain
            )
        )
    );
}
