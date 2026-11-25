using Overlayer.Resource;

namespace Overlayer.UI.Docs;

internal static partial class DocsContent {
    private static DocChapter BuildJavaScriptApi() => new(
        "js",
        UIIcon.CodeBlock,
        null,
        Tr("DOCS_JS_TITLE", "JavaScript & V8 API"),
        Tr("DOCS_JS_SUBTITLE", "Custom tags, persistent state, menu tabs, Mono runtime patching, and Unity/CLR interop running in V8"),
        DocSpecialKind.None,

        // 1. Script Discovery, Reload & IntelliSense
        new DocSection(
            Tr("DOCS_JS_SEC_DISCOVERY_TITLE", "Script Discovery, Reload & IntelliSense"),
            Tr("DOCS_JS_SEC_DISCOVERY_SUM", "Script loading paths, live reload behavior, and VS Code TypeScript IntelliSense configuration"),
            new DocParagraph(Tr(
                "DOCS_JS_DISC_P1",
                "Overlayer scans JS/Script/**/*.js recursively. Subfolders are supported; dot-folders (such as .git) and node_modules are excluded. Per-file and per-folder enable states persist in DisabledScripts.json. Scripts that fail to load or throw at runtime appear in red in the JS tab, displaying full error details on hover"
            )),
            new DocParagraph(Tr(
                "DOCS_JS_DISC_P2",
                "When Auto Reload is enabled, a FileSystemWatcher monitors JS/Script with a 300 ms debounce and reloads modified scripts automatically. Prior to V8 execution, JSScriptPreprocessor strips top-level import './impl.js' and '../impl' statements. Overlayer executes impl.js directly into the V8 engine so built-in tags such as Fps() are callable without imports, while impl.d.ts supplies full TypeScript IntelliSense in VS Code"
            )),
            new DocTable(
                Tr("DOCS_JS_DISC_TBL_H1", "File / Feature"),
                Tr("DOCS_JS_DISC_TBL_H2", "Purpose"),
                ("JS/Script/**/*.js", Tr("DOCS_JS_DISC_R1", "User script root; subfolders supported (dot-folders and node_modules excluded)")),
                ("DisabledScripts.json", Tr("DOCS_JS_DISC_R2", "Persists per-file and per-folder disabled state")),
                ("Auto Reload", Tr("DOCS_JS_DISC_R3", "FileSystemWatcher with 300 ms debounce; failed scripts highlight red with hover error details")),
                ("JS/impl.js", Tr("DOCS_JS_DISC_R4", "Executed into V8; exposes built-in tags and globals without imports")),
                ("JS/impl.d.ts", Tr("DOCS_JS_DISC_R5", "TypeScript declarations for VS Code autocomplete; import './impl.js' is stripped prior to execution"))
            )
        ),

        // 2. Registering Tags: RegisterTag
        new DocSection(
            Tr("DOCS_JS_SEC_TAGS_TITLE", "Registering Tags: RegisterTag"),
            Tr("DOCS_JS_SEC_TAGS_SUM", "Define global tags in scripts or text-local tags inside templates and Fx expressions"),
            new DocParagraph(Tr(
                "DOCS_JS_TAGS_P1",
                "RegisterTag(name, fn, options?) registers a custom tag. Calling it from a script in JS/Script creates a global tag available to all text objects and expressions. Calling RegisterTag inside a text template or Fx expression creates a text-local tag that shadows globals for that text object only, renders empty, and uses an isolated cache"
            )),
            new DocTable(
                Tr("DOCS_JS_TAGS_TBL_H1", "Option / API"),
                Tr("DOCS_JS_TAGS_TBL_H2", "Details"),
                ("Type", Tr("DOCS_JS_TAGS_R1", "TagType bitmask flags: None (0), ProcessFormat (256), JsOnly (512), Hide (65536), Advanced (16777216 — C#-only, stripped from JS tags)")),
                ("Desc", Tr("DOCS_JS_TAGS_R2", "Description displayed in Tag Explorer and tooltips")),
                ("ReturnType", Tr("DOCS_JS_TAGS_R3", "Valid values: \"number\", \"double\", \"float\", \"single\", \"decimal\", \"int\", \"int32\", \"long\", \"int64\", \"string\". Omitted or \"string\" rejects :format")),
                ("Tag.<Name>(...args)", Tr("DOCS_JS_TAGS_R4", "Proxy object that invokes any registered built-in, module, or JS tag from JavaScript"))
            ),
            new DocCode(
                Tr("DOCS_JS_TAGS_CODE_CAP", "Registering a formatted numeric tag and reading another tag via Tag proxy"),
                """
                import './impl.js';

                RegisterTag("SmoothSpeed", (scale = 1) => {
                    const fps = Tag.Fps(250);
                    return fps * scale;
                }, {
                    Type: TagType.ProcessFormat,
                    Desc: "Scaled 250ms average FPS",
                    ReturnType: "number"
                });
                """,
                DocsHighlight.Mode.Js
            )
        ),

        // 3. State & Persistence: Store & Prefs
        new DocSection(
            Tr("DOCS_JS_SEC_STATE_TITLE", "State & Persistence: Store & Prefs"),
            Tr("DOCS_JS_SEC_STATE_SUM", "In-memory session state via Store and synchronous JSON persistence via Prefs"),
            new DocTable(
                Tr("DOCS_JS_STATE_TBL_H1", "API"),
                Tr("DOCS_JS_STATE_TBL_H2", "Behavior"),
                ("Store.Get(key, fallback?)", Tr("DOCS_JS_STATE_R1", "Reads an in-memory value for key, or returns fallback (default null)")),
                ("Store.Set(key, value)", Tr("DOCS_JS_STATE_R2", "Stores value in memory and returns it")),
                ("Store.Has(key)", Tr("DOCS_JS_STATE_R3", "Returns true if key exists in Store")),
                ("Store.Remove(key)", Tr("DOCS_JS_STATE_R4", "Deletes key and returns true if it existed")),
                ("Store.Clear()", Tr("DOCS_JS_STATE_R5", "Removes all entries from Store")),
                ("Store.Keys() / Store.Count", Tr("DOCS_JS_STATE_R6", "Returns string[] of all keys / current entry count")),
                ("Prefs.Get(key, fallback?)", Tr("DOCS_JS_STATE_R7", "Reads a persisted JSON value from JS/Script/Prefs.json")),
                ("Prefs.Set(key, value)", Tr("DOCS_JS_STATE_R8", "Writes value and synchronously flushes JS/Script/Prefs.json to disk")),
                ("Prefs.Remove(key)", Tr("DOCS_JS_STATE_R9", "Removes key and synchronously saves Prefs.json"))
            ),
            new DocCallout(
                DocCalloutKind.Info,
                Tr("DOCS_JS_STATE_CALLOUT_TITLE", "Store Capacity Limit (1024 Entries)"),
                Tr("DOCS_JS_STATE_CALLOUT_TEXT", "Store retains at most 1024 entries. Inserting a new key at capacity clears the entire store rather than evicting the oldest entry. Use Prefs with namespaced keys (e.g. \"MyScript.option\") for persistent settings")
            )
        ),

        // 4. Custom Menu Tabs: AddTab & JSUIBuilder
        new DocSection(
            Tr("DOCS_JS_SEC_UI_TITLE", "Custom Menu Tabs: AddTab & JSUIBuilder"),
            Tr("DOCS_JS_SEC_UI_SUM", "Build native Overlayer settings tabs from JS using live O5Kit controls"),
            new DocParagraph(Tr(
                "DOCS_JS_UI_P1",
                "AddTab(name, build, options?) creates a sidebar tab and returns a numeric handle. options.icon accepts a UIIcon name (case-insensitive, optional 128 suffix) or UISprite name, defaulting to CodeBlock. RebuildTab(handle, newBuild?) clears and re-invokes the tab builder; RemoveTab(handle) destroys the tab. Tabs are automatically disposed when their owning script reloads, is disabled, or is deleted"
            )),
            new DocParagraph(Tr(
                "DOCS_JS_UI_P2",
                "Each JSUIBuilder method appends a control and returns the live O5Kit control instance (e.g., invoke .Set(value) on a slider or toggle). Defaults: Text(text, size = 18), Slider(text, min, max, value, onChanged, format = \"0.##\"), Row(spacing = 8). builder.Root exposes the container Transform for raw Unity UI parenting"
            )),
            new DocBadges(
                Tr("DOCS_JS_UI_BADGES", "JSUIBuilder Members"),
                false,
                "Header", "Text", "Button", "Toggle", "Slider", "Input",
                "Dropdown", "Color", "Row", "Card", "Clear", "Root"
            ),
            new DocCode(
                Tr("DOCS_JS_UI_CODE_CAP", "Creating a custom settings tab with JSUIBuilder and Prefs"),
                """
                const tabId = AddTab("My Plugin", ui => {
                    ui.Header("General");
                    const card = ui.Card("Display");
                    const row = card.Row(8);
                    row.Toggle("Enabled", Prefs.Get("MyPlugin.on", true), v => Prefs.Set("MyPlugin.on", v));
                    const s = card.Slider("Scale", 0.5, 2.0, Prefs.Get("MyPlugin.scale", 1.0),
                        v => Prefs.Set("MyPlugin.scale", v), "0.00");
                    ui.Button("Reset Scale", () => s.Set(1.0));
                }, { icon: "Gear128" });
                """,
                DocsHighlight.Mode.Js
            )
        ),

        // 5. Runtime Patching: AddPatch & RemovePatch
        new DocSection(
            Tr("DOCS_JS_SEC_PATCH_TITLE", "Runtime Patching: AddPatch & RemovePatch"),
            Tr("DOCS_JS_SEC_PATCH_SUM", "Hook C# methods at runtime with Harmony-backed prefix and postfix callbacks"),
            new DocCallout(
                DocCalloutKind.Warning,
                Tr("DOCS_JS_PATCH_WARN_TITLE", "Mono Builds Only"),
                Tr("DOCS_JS_PATCH_WARN_TEXT", "AddPatch and RemovePatch are supported solely on Mono builds and are unavailable on IL2CPP builds")
            ),
            new DocParagraph(Tr(
                "DOCS_JS_PATCH_P1",
                "AddPatch(target, options) patches a C# method specified as \"Type::Method\" or \"Type::Method(ArgType1, ...)\" and returns a handle (or -1 on error). When the parameter list is omitted, Overlayer selects the matching overload based on callback arity. A parameter literally named __instance is the sole Harmony-style special parameter and may appear at any position (bound to the target instance, or null for static methods)"
            )),
            new DocParagraph(Tr(
                "DOCS_JS_PATCH_P2",
                "Callbacks support both expanded form — prefix(a, b, ...) and postfix(a, b, ..., result) — and packed form — prefix(args) and postfix(args, result). Returning false from a prefix skips the original method; returning { result: x } skips a non-void original with a custom return value. Returning a non-null value from a postfix replaces the return value. A built-in recursion guard prevents infinite re-entry. RemovePatch(handle) unpatches and returns bool"
            )),
            new DocCode(
                Tr("DOCS_JS_PATCH_CODE_CAP", "Expanded arity, __instance, return override, and packed args patterns"),
                """
                // 1. Expanded form with __instance and return override
                const h1 = AddPatch("PlayerController::TakeDamage", {
                    prefix: (amount, __instance) => {
                        if (__instance.isInvincible) return false; // skip original
                        if (amount > 999) return { result: 999 };  // skip & override return
                    },
                    postfix: (amount, result, __instance) => {
                        return result * 2; // non-null postfix return replaces result
                    }
                });

                // 2. Packed form (args array)
                const h2 = AddPatch("ScoreManager::AddPoints(int,bool)", {
                    postfix: (args, result) => {
                        Log.Msg(`Added ${args[0]} -> total ${result}`);
                    }
                });

                RemovePatch(h1); // returns true if removed
                """,
                DocsHighlight.Mode.Js
            )
        ),

        // 6. Unity, Clr Interop & Host Types
        new DocSection(
            Tr("DOCS_JS_SEC_INTEROP_TITLE", "Unity, Clr Interop & Host Types"),
            Tr("DOCS_JS_SEC_INTEROP_SUM", "Main-thread scheduling, Unity helpers, vector/color coercion, reflection via Clr, and exposed UnityEngine types"),
            new DocCallout(
                DocCalloutKind.Info,
                Tr("DOCS_JS_UNITY_CALLOUT_TITLE", "Scripts Load Off the Main Thread"),
                Tr("DOCS_JS_UNITY_CALLOUT_TEXT", "Script files load on a background thread. Wrap Unity API calls executed during script load inside Unity.NextTick(() => ...) to schedule execution on the Unity main thread")
            ),
            new DocTable(
                Tr("DOCS_JS_INTEROP_TBL_H1", "Global / Method"),
                Tr("DOCS_JS_INTEROP_TBL_H2", "Description"),
                ("Unity.FindObjectOfType / FindObjectsOfType(typeName)", Tr("DOCS_JS_INT_R1", "Finds scene objects by C# type name string")),
                ("Unity.GetComponent / GetComponents / AddComponent / HasComponent", Tr("DOCS_JS_INT_R2", "Component lookup/addition on a GameObject or Component by type name string")),
                ("Unity.IsValid(obj)", Tr("DOCS_JS_INT_R3", "Reliable alive check (destroyed Unity objects are not JS null)")),
                ("Unity.NextTick(fn)", Tr("DOCS_JS_INT_R4", "Enqueues fn to run on the Unity main thread next frame")),
                ("Unity.Repeat(sec, fn) / CancelTick(handle)", Tr("DOCS_JS_INT_R5", "Runs fn every sec seconds on main thread (0 = every frame); CancelTick returns bool")),
                ("Unity.Vec2 / Vec3 / Vec4 / Rgba", Tr("DOCS_JS_INT_R6", "Float-coercion helpers: Vec2(x,y), Vec3(x,y,z), Vec4(x,y,z,w), Rgba(r,g,b,a=1)")),
                ("Clr.Get / Set / Call / Invoke", Tr("DOCS_JS_INT_R7", "Reflective field/property/method access with numeric coercion on Set; Invoke(typeName, method, ...args) static shorthand")),
                ("Clr.TryGet / TrySet / TryCall / Create", Tr("DOCS_JS_INT_R8", "Exception-safe variants returning null/false on failure; Create(typeName, ...args) constructs instances")),
                ("Clr.Prepare / TryPrepare(target, member)", Tr("DOCS_JS_INT_R9", "Returns a cached ClrMember with .Get(), .Set(v), and .Call(...args)")),
                ("Log / console", Tr("DOCS_JS_INT_R10", "Log.Msg/Wrn/Err and Log.Log/Warn/Error (console aliases the same logger)"))
            ),
            new DocBadges(
                Tr("DOCS_JS_HOST_BADGES", "Exposed UnityEngine Host Types"),
                false,
                "GameObject", "Transform", "RectTransform", "Component", "Behaviour",
                "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Color32",
                "Mathf", "Time", "Random", "UnityObject", "SceneManager", "Scene",
                "Application", "Screen", "Input", "O5Input", "KeyCode", "Cursor",
                "CursorLockMode", "Camera", "Canvas", "AudioSource"
            )
        )
    );
}
