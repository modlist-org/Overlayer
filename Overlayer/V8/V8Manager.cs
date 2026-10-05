using Microsoft.ClearScript.V8;
using Overlayer.Compat.Interface;
using Overlayer.Core;
using Overlayer.Tag.Core;
using Overlayer.Tag.Runtime;
using Overlayer.V8.Scripting.Diagnostic;
using Overlayer.V8.Scripting.Tag;
using System.Text;

namespace Overlayer.V8;

public class V8Manager : IRuntimeService {
    public const int FxScriptTimeoutMilliseconds = 500;
    public const int FxScriptBackoffMilliseconds = 5000;

    private readonly object _engineLock = new();
    private V8ScriptEngine _engine;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    private readonly JSScriptLoader _scriptLoader = new();
    public IReadOnlyList<JSDiagnostic> LoaderDiagnostics => _scriptLoader.Diagnostics;

    public IReadOnlyList<string> ScriptFiles => _scriptLoader.GetScriptFiles();

    public IReadOnlyList<string> ScriptFileTags(string filePath) => _scriptLoader.GetFileTags(filePath);

    public Task InitializationTask { get; private set; }
    private FileSystemWatcher _watcher;

    public const string ImplFileName = "impl.js";
    public const string ImplDtsFileName = "impl.d.ts";
    public const string ScriptFolderName = "Script";

    public string ScriptFolderPath { get; private set; }
    public string ImplFilePath { get; private set; }
    public string ImplDtsFilePath { get; private set; }

    public V8ScriptEngine Engine {
        get {
            lock(_engineLock) {
                return _engine;
            }
        }
    }

    public void Initialize() => InitializationTask = InitializeAsync();

    public async Task InitializeAsync() {
        ImplFilePath = Path.Combine(MainCore.Paths.JSPath, ImplFileName);
        ImplDtsFilePath = Path.Combine(MainCore.Paths.JSPath, ImplDtsFileName);
        ScriptFolderPath = Path.Combine(MainCore.Paths.JSPath, ScriptFolderName);

        if(!Directory.Exists(ScriptFolderPath)) {
            Directory.CreateDirectory(ScriptFolderPath);
            MainCore.Log.Msg($"[{nameof(V8Manager)}] Script folder created at: {ScriptFolderPath}");
        }

        lock(_engineLock) {
            _engine = new V8ScriptEngine();
            BindEngine(_engine);
        }

        await Task.CompletedTask;
    }

    public async Task LoadScriptsAsync() {
        try {
            if(await _scriptLoader.LoadAllScriptsAsync(ScriptFolderPath, _engine)) {
                foreach(var diag in LoaderDiagnostics) {
                    MainCore.Log.Msg(diag.ToString());
                }
            }
        } catch(Exception e) {
            MainCore.Log.Err($"[{nameof(V8Manager)}] Script load failed: {e.Message}");
        }
        GenerateImplJs(force: true);
        UpdateWatcher();
    }

    private void BindEngine(V8ScriptEngine engine) {
        engine.AddHostObject(nameof(TagAccessHelper), new TagAccessHelper());
        engine.AddHostObject(nameof(Store), Store);
        engine.AddHostObject("Clr", new Scripting.Clr.ClrAccess());
        engine.AddHostObject("Unity", new Scripting.Unity.UnityAccess());
        engine.AddHostType("GameObject", typeof(UnityEngine.GameObject));
        engine.AddHostType("Transform", typeof(UnityEngine.Transform));
        engine.AddHostType("RectTransform", typeof(UnityEngine.RectTransform));
        engine.AddHostType("Component", typeof(UnityEngine.Component));
        engine.AddHostType("Behaviour", typeof(UnityEngine.Behaviour));
        engine.AddHostType("Vector2", typeof(UnityEngine.Vector2));
        engine.AddHostType("Vector3", typeof(UnityEngine.Vector3));
        engine.AddHostType("Vector4", typeof(UnityEngine.Vector4));
        engine.AddHostType("Quaternion", typeof(UnityEngine.Quaternion));
        engine.AddHostType("Color", typeof(UnityEngine.Color));
        engine.AddHostType("Color32", typeof(UnityEngine.Color32));
        engine.AddHostType("Mathf", typeof(UnityEngine.Mathf));
        engine.AddHostType("Time", typeof(UnityEngine.Time));
        engine.AddHostType("Random", typeof(UnityEngine.Random));
        engine.AddHostType("UnityObject", typeof(UnityEngine.Object));
        engine.AddHostType("SceneManager", typeof(UnityEngine.SceneManagement.SceneManager));
        engine.AddHostType("Scene", typeof(UnityEngine.SceneManagement.Scene));
        engine.AddHostType("Application", typeof(UnityEngine.Application));
        engine.AddHostType("Screen", typeof(UnityEngine.Screen));
        engine.AddHostType("Input", typeof(UnityEngine.Input));
        engine.AddHostType("KeyCode", typeof(UnityEngine.KeyCode));
        engine.AddHostType("Cursor", typeof(UnityEngine.Cursor));
        engine.AddHostType("CursorLockMode", typeof(UnityEngine.CursorLockMode));
        engine.AddHostType("Camera", typeof(UnityEngine.Camera));
        engine.AddHostType("Canvas", typeof(UnityEngine.Canvas));
        engine.AddHostType("AudioSource", typeof(UnityEngine.AudioSource));
        var log = new Scripting.JSLog();
        engine.AddHostObject("Log", log);
        engine.AddHostObject("console", log);
        engine.Execute(JSTagRegistrationHost.TagTypeScript);
    }

    public FxStore Store { get; } = new();

    internal object InvokeCallback(Microsoft.ClearScript.ScriptObject fn, object[] callArgs) {
        lock(_engineLock) {
            if(_engine == null) {
                return null;
            }
            return fn.Invoke(false, callArgs);
        }
    }

    public void Reset() {
        lock(_engineLock) {
            ClearFxScriptCache();
            _engine?.Dispose();
            _engine = new V8ScriptEngine();
            BindEngine(_engine);
        }
        Scripting.Patch.JSPatchManager.RemoveAll();
        LoadImplJs();
    }

    public async Task ReloadScriptsAsync() {
        await _reloadGate.WaitAsync();
        try {
            var preview = await Task.Run(() => _scriptLoader.PreviewChanges(ScriptFolderPath));
            if(preview.changed.Count == 0 && preview.removed.Count == 0) {
                return;
            }
            lock(_engineLock) {
                ClearFxScriptCache();
                _engine.Dispose();
                _engine = new V8ScriptEngine();
                BindEngine(_engine);
            }

            TagCache.Instance.Clear();

            if(await _scriptLoader.LoadAllScriptsAsync(ScriptFolderPath, _engine)) {
                foreach(var diag in LoaderDiagnostics) {
                    MainCore.Log.Msg(diag.ToString());
                }
                GenerateImplJs(false);
            }
        } finally {
            _reloadGate.Release();
        }
    }

    public void GenerateImplJs(bool force = false) {
        var tags = TagManager.GetAllTags();
        var jsContent = BuildImplJsContent(tags);
        var dtsContent = BuildImplDtsContent(tags);

        lock(_engineLock) {
            try {
                bool jsUpToDate = !force && File.Exists(ImplFilePath) &&
                    File.ReadAllText(ImplFilePath) == jsContent;
                if(jsUpToDate) {
                    _engine.Execute(jsContent);
                } else {
                    File.WriteAllText(ImplFilePath, jsContent);
                    _engine.Execute(jsContent);
                    MainCore.Log.Msg($"[{nameof(V8Manager)}] {ImplFileName} changed, synced and loaded.");
                }

                if(!string.IsNullOrEmpty(ImplDtsFilePath)) {
                    bool dtsUpToDate = !force && File.Exists(ImplDtsFilePath) &&
                        File.ReadAllText(ImplDtsFilePath) == dtsContent;
                    if(!dtsUpToDate) {
                        File.WriteAllText(ImplDtsFilePath, dtsContent);
                        MainCore.Log.Msg($"[{nameof(V8Manager)}] {ImplDtsFileName} changed, synced.");
                    }
                }
            } catch(Exception ex) {
                MainCore.Log.Err($"[{nameof(V8Manager)}] Failed to sync {ImplFileName}: {ex.Message}");
            }
        }
    }

    private string BuildImplJsContent(IEnumerable<TagCore> tags) {
        var sb = new StringBuilder();
        sb.AppendLine("/* Auto-generated by Overlayer, DO NOT EDIT */\n");
        sb.AppendLine("globalThis.Tag = new Proxy({}, {");
        sb.AppendLine("    get: function(target, prop) {");
        sb.AppendLine("        return function(...args) { return TagAccessHelper.Get(prop, ...args); };");
        sb.AppendLine("    }");
        sb.AppendLine("});\n");

        sb.AppendLine("/* Overlayer Functions */\n");

        sb.AppendLine("/* TagType bitmask constants (see TagCore.TagType).");
        sb.AppendLine("   Use in RegisterTag options, e.g. { Type: TagType.ProcessFormat }");
        sb.AppendLine("   or combine with bitwise OR: { Type: TagType.ProcessFormat | TagType.BlockOnPaused } */");
        sb.AppendLine(JSTagRegistrationHost.TagTypeScript + "\n");

        sb.AppendLine("/**");
        sb.AppendLine(" * Registers a new custom tag with the Overlayer engine.");
        sb.AppendLine(" * ");
        sb.AppendLine(" * @param {string} name - The unique name of the tag.");
        sb.AppendLine(" * @param {Function} func - The logic to execute.");
        sb.AppendLine(" * @param {Object} [options] - Configuration object.");
        sb.AppendLine(" * @param {number} [options.Type] - TagType bitmask (e.g. TagType.ProcessFormat, or TagType.ProcessFormat | TagType.BlockOnPaused).");
        sb.AppendLine(" * Use TagType.ProcessFormat to allow a trailing format argument (e.g. {Tag:0.##}).");
        sb.AppendLine(" * @param {string} [options.ReturnType] - Declared return type for format validation.");
        sb.AppendLine(" * One of 'number', 'float', 'decimal', 'int', 'long', 'string'.");
        sb.AppendLine(" * Only numeric types allow a format argument; otherwise it is a compile error.");
        sb.AppendLine(" * @param {string} [options.Desc] - Description of the tag.");
        sb.AppendLine(" */");
        sb.AppendLine("globalThis.RegisterTag = function(name, func, options) {};\n");

        sb.AppendLine("/* Global Store: share values between Fx expressions. */");
        sb.AppendLine("/* Store.Set(key, value): save a value, returns it. */");
        sb.AppendLine("/* Store.Get(key, fallback): value or fallback. */");
        sb.AppendLine("/* Store.Has(key) / Store.Remove(key) / Store.Clear() / Store.Keys() / Store.Count. */\n");

        sb.AppendLine("/* CLR access: Clr.Create(\"System.Random\") makes an instance. */");
        sb.AppendLine("/* Clr.Get(targetOrType, member) / Clr.Set(targetOrType, member, value). */");
        sb.AppendLine("/* Clr.Call(targetOrType, method, ...args) / Clr.Invoke(typeName, method, ...args). */");
        sb.AppendLine("/* Statics take a type-name string, e.g. Clr.Call(\"System.Math\", \"Max\", 1, 2). */");
        sb.AppendLine("/* Fast path: var m = Clr.Prepare(targetOrType, member); m.Get(); m.Set(v); m.Call(...args). */\n");

        sb.AppendLine("/* Unity API, real names (main thread only). Host types: */");
        sb.AppendLine("/* GameObject: new GameObject(name), GameObject.Find(name), FindWithTag, FindGameObjectsWithTag. */");
        sb.AppendLine("/*   go.name, go.SetActive(on), go.transform, go.AddComponent(Type), go.GetComponent(Type). */");
        sb.AppendLine("/* UnityObject (= UnityEngine.Object; renamed: JS already has `Object`): */");
        sb.AppendLine("/*   UnityObject.Destroy(obj, delay?), UnityObject.DontDestroyOnLoad(obj), UnityObject.Instantiate(obj). */");
        sb.AppendLine("/* Structs: new Vector3(x, y, z), new Vector2(x, y), new Color(r, g, b, a?), Quaternion.Euler(x, y, z). */");
        sb.AppendLine("/* Statics: Time.deltaTime, Mathf.Clamp(v, a, b), Random.Range(a, b). transform.position etc. assignable. */");
        sb.AppendLine("/* Unity glue (string-named types + main-thread defer): */");
        sb.AppendLine("/* Unity.FindObjectsOfType(\"Rigidbody\") / Unity.FindObjectOfType(\"Rigidbody\"). */");
        sb.AppendLine("/* Unity.AddComponent(go, \"Rigidbody\") / Unity.GetComponent / Unity.GetComponents / Unity.HasComponent. */");
        sb.AppendLine("/* Unity.IsValid(obj): dead Unity objects are NOT js null, check with this. */");
        sb.AppendLine("/* Unity.NextTick(fn): run fn on the main thread next frame (use from script load time). */");
        sb.AppendLine("/* Unity.Repeat(seconds, fn): run fn every N seconds (0 = every frame), returns a handle. */");
        sb.AppendLine("/* Unity.CancelTick(handle): stop a Repeat. Repeat callbacks cost like per-frame patches: keep them light. */");
        sb.AppendLine("/* Scenes: SceneManager.LoadScene(name), SceneManager.GetActiveScene(). Scripts: Input.GetKey(KeyCode.Space). */");
        sb.AppendLine("/* Screen: Screen.SetResolution(w, h, false), Screen.fullScreen, Application.targetFrameRate = 144. */");
        sb.AppendLine("/* Camera.main.WorldToScreenPoint(pos), Cursor.visible, Time.timeScale, Mathf.Clamp, Random.Range. */\n");

        sb.AppendLine("/* Logging: Log.Msg(x) / Log.Wrn(x) / Log.Err(x) -> MelonLoader log as [JS]. */");
        sb.AppendLine("/* console.log / console.warn / console.error map to the same. */\n");

        sb.AppendLine("/**");
        sb.AppendLine(" * Patches a game/mod method with JS prefix/postfix callbacks. Mono-only.");
        sb.AppendLine(" * ");
        sb.AppendLine(" * @param {string} target - \"Type::Method\" (auto-pick when unique) or \"Type::Method(Arg1, Arg2)\".");
        sb.AppendLine(" * @param {Object} options - { prefix, postfix } (at least one).");
        sb.AppendLine(" * prefix(args) may mutate args; return false to skip the original,");
        sb.AppendLine(" * return { result: x } to skip with result x. Expanded (a, b) form matches overload arity.");
        sb.AppendLine(" * postfix(args, result): return non-undefined to replace the result.");
        sb.AppendLine(" * @returns {number} Patch handle for RemovePatch, or -1 on error. Unpatched automatically on script reload.");
        sb.AppendLine(" */");
        sb.AppendLine("globalThis.AddPatch = function(target, options) {};");
        sb.AppendLine("globalThis.RemovePatch = function(handle) {};\n");

        sb.AppendLine("/* Tags */\n");

        foreach(var tag in tags.OrderBy(t => t.Name)) {
            var paramNames = tag.Parameters.Select(p => p.Name).ToArray();
            string paramList = string.Join(", ", paramNames);
            string returnType = MapToJsType(tag.ReturnType);

            sb.AppendLine("/**");
            if(!string.IsNullOrEmpty(tag.Description)) {
                sb.AppendLine($" * {tag.Description.Replace("\n", "\n * ")}");
            }

            foreach(var p in tag.Parameters) {
                sb.AppendLine($" * @param {{ {p.Name}: {MapToJsType(p.ParameterType)} }}");
            }

            sb.AppendLine($" * @returns {{{returnType}}} */");
            sb.AppendLine($"function {tag.Name}({paramList}) {{ return Tag.{tag.Name}({paramList}); }}\n");
        }
        return sb.ToString();
    }

    private string BuildImplDtsContent(IEnumerable<TagCore> tags) {
        var sb = new StringBuilder();
        sb.AppendLine("/* Auto-generated by Overlayer, DO NOT EDIT */");
        sb.AppendLine("/* TypeScript declarations for external authoring.");
        sb.AppendLine("   Write .ts referencing this file, then compile with tsc");
        sb.AppendLine("   and drop the emitted .js into Script/. Runtime executes JS only. */");
        sb.AppendLine("declare const TagType: {");
        sb.AppendLine("    readonly None: 0;");
        sb.AppendLine("    readonly BlockOnNotPlaying: 1;");
        sb.AppendLine("    readonly BlockOnPaused: 2;");
        sb.AppendLine("    readonly BlockOnAll: 3;");
        sb.AppendLine("    readonly ProcessFormat: 256;");
        sb.AppendLine("    readonly JsOnly: 512;");
        sb.AppendLine("    readonly Hide: 65536;");
        sb.AppendLine("    readonly Advanced: 16777216;");
        sb.AppendLine("};");
        sb.AppendLine("interface RegisterTagOptions {");
        sb.AppendLine("    Type?: number;");
        sb.AppendLine("    Desc?: string;");
        sb.AppendLine("}");
        sb.AppendLine("declare function RegisterTag(name: string, func: (...args: any[]) => any, options?: RegisterTagOptions): void;");
        sb.AppendLine("interface FxStore {");
        sb.AppendLine("    Get(key: string, fallback?: any): any;");
        sb.AppendLine("    Set(key: string, value: any): any;");
        sb.AppendLine("    Has(key: string): boolean;");
        sb.AppendLine("    Remove(key: string): boolean;");
        sb.AppendLine("    Clear(): void;");
        sb.AppendLine("    Keys(): string[];");
        sb.AppendLine("    readonly Count: number;");
        sb.AppendLine("}");
        sb.AppendLine("declare const Store: FxStore;");
        sb.AppendLine("interface ClrAccess {");
        sb.AppendLine("    Create(typeName: string, ...args: any[]): any;");
        sb.AppendLine("    Get(targetOrType: any, member: string): any;");
        sb.AppendLine("    Set(targetOrType: any, member: string, value: any): void;");
        sb.AppendLine("    Call(targetOrType: any, method: string, ...args: any[]): any;");
        sb.AppendLine("    Invoke(typeName: string, method: string, ...args: any[]): any;");
        sb.AppendLine("    Prepare(targetOrType: any, member: string): ClrMember;");
        sb.AppendLine("    TryGet(targetOrType: any, member: string): any;");
        sb.AppendLine("    TrySet(targetOrType: any, member: string, value: any): boolean;");
        sb.AppendLine("    TryCall(targetOrType: any, method: string, ...args: any[]): any;");
        sb.AppendLine("    TryPrepare(targetOrType: any, member: string): ClrMember;");
        sb.AppendLine("}");
        sb.AppendLine("interface ClrMember {");
        sb.AppendLine("    Get(): any;");
        sb.AppendLine("    Set(value: any): void;");
        sb.AppendLine("    Call(...args: any[]): any;");
        sb.AppendLine("}");
        sb.AppendLine("declare const Clr: ClrAccess;");
        sb.AppendLine("interface UnityAccess {");
        sb.AppendLine("    FindObjectsOfType(typeName: string): any[];");
        sb.AppendLine("    FindObjectOfType(typeName: string): any;");
        sb.AppendLine("    AddComponent(obj: any, typeName: string): any;");
        sb.AppendLine("    GetComponent(obj: any, typeName: string): any;");
        sb.AppendLine("    GetComponents(obj: any, typeName: string): any[];");
        sb.AppendLine("    HasComponent(obj: any, typeName: string): boolean;");
        sb.AppendLine("    IsValid(obj: any): boolean;");
        sb.AppendLine("    NextTick(fn: (...args: any[]) => any): boolean;");
        sb.AppendLine("    Repeat(seconds: number, fn: (...args: any[]) => any): number;");
        sb.AppendLine("    CancelTick(handle: number): boolean;");
        sb.AppendLine("}");
        sb.AppendLine("declare const Unity: UnityAccess;");
        sb.AppendLine("declare const GameObject: any;");
        sb.AppendLine("declare const Transform: any;");
        sb.AppendLine("declare const RectTransform: any;");
        sb.AppendLine("declare const Component: any;");
        sb.AppendLine("declare const Behaviour: any;");
        sb.AppendLine("declare const Vector2: any;");
        sb.AppendLine("declare const Vector3: any;");
        sb.AppendLine("declare const Vector4: any;");
        sb.AppendLine("declare const Quaternion: any;");
        sb.AppendLine("declare const Color: any;");
        sb.AppendLine("declare const Color32: any;");
        sb.AppendLine("declare const Mathf: any;");
        sb.AppendLine("declare const Time: any;");
        sb.AppendLine("declare const Random: any;");
        sb.AppendLine("declare const UnityObject: any;");
        sb.AppendLine("declare const SceneManager: any;");
        sb.AppendLine("declare const Scene: any;");
        sb.AppendLine("declare const Application: any;");
        sb.AppendLine("declare const Screen: any;");
        sb.AppendLine("declare const Input: any;");
        sb.AppendLine("declare const KeyCode: any;");
        sb.AppendLine("declare const Cursor: any;");
        sb.AppendLine("declare const CursorLockMode: any;");
        sb.AppendLine("declare const Camera: any;");
        sb.AppendLine("declare const Canvas: any;");
        sb.AppendLine("declare const AudioSource: any;");
        sb.AppendLine("interface TagNamespace {");
        sb.AppendLine("    [key: string]: (...args: any[]) => any;");
        sb.AppendLine("}");
        sb.AppendLine("declare const Tag: TagNamespace;");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach(var tag in tags.OrderBy(t => t.Name)) {
            if(string.IsNullOrEmpty(tag.Name) || !seen.Add(tag.Name)) {
                continue;
            }
            if(!IsValidIdentifier(tag.Name) || IsReservedGlobal(tag.Name)) {
                continue;
            }
            sb.AppendLine("/**");
            if(!string.IsNullOrEmpty(tag.Description)) {
                sb.AppendLine($" * {EscapeDocComment(tag.Description).Replace("\n", "\n * ")}");
            }
            foreach(var p in tag.Parameters) {
                sb.AppendLine($" * @param {SanitizeParamName(p.Name)}");
            }
            sb.AppendLine(" */");
            var paramList = string.Join(", ", tag.Parameters.Select((p, i) =>
                $"{SanitizeParamName(p.Name, i)}{(p.HasDefaultValue ? "?" : "")}: {MapToJsType(p.ParameterType)}"));
            sb.AppendLine($"declare function {tag.Name}({paramList}): {MapToJsType(tag.ReturnType)};");
        }
        return sb.ToString();
    }

    private static bool IsValidIdentifier(string name) {
        if(string.IsNullOrEmpty(name)) {
            return false;
        }
        if(!(char.IsLetter(name[0]) || name[0] == '_' || name[0] == '$')) {
            return false;
        }
        return name.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_' || c == '$');
    }

    private static bool IsReservedGlobal(string name)
        => name is "Tag" or "Store" or "RegisterTag" or "TagType";

    private static string SanitizeParamName(string name, int index = -1) {
        if(IsValidIdentifier(name)) {
            return name;
        }
        return index >= 0 ? $"arg{index + 1}" : "arg";
    }

    private static string EscapeDocComment(string value)
        => value.Replace("*/", "* /");

    private string MapToJsType(Type type) {
        if(type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(long) ||
            type == typeof(byte) || type == typeof(short) || type == typeof(uint) || type == typeof(ulong) ||
            type == typeof(ushort) || type == typeof(decimal)) {
            return "number";
        }

        if(type == typeof(string) || type.IsEnum) {
            return "string";
        }

        if(type == typeof(bool)) {
            return "boolean";
        }

        return "any";
    }

    private readonly Dictionary<string, (V8ScriptEngine Engine, V8Script Script, string Error)> _fxScriptCache = new();
    private readonly Dictionary<string, string> _fxRuntimeErrors = new();
    private readonly Dictionary<string, long> _fxTimeoutBackoff = new();
    private long _evalSeq;
    private long _evalActive = -1;

    private (V8Script Script, string Error) CompileFx(string code) {
        if (_fxScriptCache.TryGetValue(code, out var cached) && ReferenceEquals(cached.Engine, _engine)) {
            return (cached.Script, cached.Error);
        }

        V8Script script = null;
        string error = null;
        try {
            script = _engine.Compile(code);
        } catch (Exception ex) {
            error = ex.Message;
        }

        if (_fxScriptCache.Count > 256) {
            ClearFxScriptCache();
        }

        _fxScriptCache[code] = (_engine, script, error);
        return (script, error);
    }

    public string GetFxCompileError(string code) {
        if (string.IsNullOrWhiteSpace(code)) {
            return null;
        }

        lock(_engineLock) {
            if (_engine == null) {
                return null;
            }

            try {
                return CompileFx(code).Error;
            } catch {
                return null;
            }
        }
    }

    public bool TryEvaluateFx(string code, out object result) {
        result = null;
        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        lock(_engineLock) {
            if (_engine == null) {
                return false;
            }

            try {
                if (IsBackedOff(code)) {
                    return false;
                }

                var compiled = CompileFx(code);
                if (compiled.Script == null) {
                    return false;
                }

                var engine = _engine;
                return RunGuarded(code, engine, () => engine.Evaluate(compiled.Script), out result);
            } catch {
                result = null;
                return false;
            }
        }
    }

    private bool IsBackedOff(string code) {
        if (_fxTimeoutBackoff.TryGetValue(code, out var bannedUntil)
            && DateTime.UtcNow.Ticks < bannedUntil) {
            return true;
        }

        _fxTimeoutBackoff.Remove(code);
        return false;
    }

    private bool RunGuarded(string code, V8ScriptEngine engine, Func<object> run, out object result) {
        result = null;
        // Generation tag: the timeout may only interrupt the eval that
        // actually timed out. Without this, a late timer from eval A fires
        // engine-wide Interrupt() while an innocent eval B runs, B throws,
        // and B's code eats a 5s backoff (e.g. a held key shows released).
        long seq = Interlocked.Increment(ref _evalSeq);
        Volatile.Write(ref _evalActive, seq);
        int running = 1;
        bool timedOut = false;
        using var timeout = new CancellationTokenSource();
        Task.Delay(FxScriptTimeoutMilliseconds, timeout.Token).ContinueWith(task => {
            if(!task.IsCanceled && Interlocked.CompareExchange(ref running, 0, 1) == 1
                && Volatile.Read(ref _evalActive) == seq) {
                timedOut = true;
                try {
                    engine.Interrupt();
                } catch {
                }
            }
        }, TaskScheduler.Default);

        try {
            result = run();
        } catch(Exception ex) {
            result = null;
            if(timedOut) {
                RecordTimeout(code);
            } else {
                RecordRuntimeError(code, ex.Message);
            }

            return false;
        } finally {
            timeout.Cancel();
            Interlocked.Exchange(ref running, 0);
            if(Volatile.Read(ref _evalActive) == seq) {
                Volatile.Write(ref _evalActive, -1);
            }
        }

        if (timedOut) {
            result = null;
            RecordTimeout(code);
            return false;
        }

        _fxTimeoutBackoff.Remove(code);
        _fxRuntimeErrors.Remove(code);
        return result != null;
    }

    private void RecordRuntimeError(string code, string message) {
        try {
            message ??= "error";
            int newline = message.IndexOf('\n');
            if (newline >= 0) {
                message = message[..newline];
            }

            if (message.Length > 200) {
                message = message[..200];
            }

            if (_fxRuntimeErrors.Count > 256) {
                _fxRuntimeErrors.Clear();
            }

            _fxRuntimeErrors[code] = message;
        } catch {
        }
    }

    private void RecordTimeout(string code) {
        try {
            _fxTimeoutBackoff[code] = DateTime.UtcNow.Ticks + FxScriptBackoffMilliseconds * 10000L;
            if (_fxTimeoutBackoff.Count > 256) {
                _fxTimeoutBackoff.Clear();
            }

            string preview = code.Replace('\n', ' ');
            if (preview.Length > 120) {
                preview = preview[..120];
            }

            _fxRuntimeErrors[code] =
                $"Script timed out after {FxScriptTimeoutMilliseconds}ms.";
            MainCore.Log.Wrn($"[{nameof(V8Manager)}] Fx script timed out after {FxScriptTimeoutMilliseconds}ms, cooling down: {preview}");
        } catch {
        }
    }

    public string GetFxRuntimeError(string code) {
        if (string.IsNullOrWhiteSpace(code)) {
            return null;
        }

        lock(_engineLock) {
            try {
                if (_fxRuntimeErrors.TryGetValue(code, out var message)) {
                    return message;
                }
            } catch {
            }

            return null;
        }
    }

    private void ClearFxScriptCache() {
        foreach(var entry in _fxScriptCache.Values) {
            try {
                entry.Script?.Dispose();
            } catch {
            }
        }

        _fxScriptCache.Clear();
        _fxRuntimeErrors.Clear();
        _fxTimeoutBackoff.Clear();
    }

    public void LoadImplJs() {        lock(_engineLock) {
            if(File.Exists(ImplFilePath)) {
                try {
                    _engine.Execute(File.ReadAllText(ImplFilePath));
                } catch(Exception ex) {
                    MainCore.Log.Wrn($"[{nameof(V8Manager)}] Load failed: {ex.Message}");
                }
            }
        }
    }

    public void ReloadScriptFile(string filePath) {
        lock(_engineLock) {
            if(_engine == null) {
                return;
            }
            _scriptLoader.ReloadFile(filePath, _engine);
            foreach(var diag in LoaderDiagnostics) {
                MainCore.Log.Msg(diag.ToString());
            }
            GenerateImplJs(false);
        }
    }

    public void UpdateWatcher() {
        bool enabled = MainCore.Conf.EnableJSScriptWatcher;
        if(enabled && _watcher == null) {
            _watcher = new FileSystemWatcher(ScriptFolderPath, "*.js") {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            _watcher.Changed += OnScriptChanged;
            _watcher.Created += OnScriptChanged;
            _watcher.Deleted += OnScriptChanged;
            _watcher.Renamed += OnScriptChanged;
            _watcher.EnableRaisingEvents = true;
        } else if(!enabled && _watcher != null) {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private CancellationTokenSource _watchDebounce;
    private void OnScriptChanged(object sender, FileSystemEventArgs e) {
        _watchDebounce?.Cancel();
        _watchDebounce?.Dispose();
        var cts = _watchDebounce = new CancellationTokenSource();
        _ = Task.Delay(300, cts.Token).ContinueWith(async t => {
            if(t.IsCanceled) {
                return;
            }
            try {
                await ReloadScriptsAsync();
            } catch(Exception ex) {
                MainCore.Log.Err($"[{nameof(V8Manager)}] Script reload failed: {ex.Message}");
            }
        }, TaskScheduler.Default);
    }

    public void Dispose() {
        lock(_engineLock) {
            ClearFxScriptCache();
            _watcher?.Dispose();
            _engine?.Dispose();
            _engine = null;
        }
        try {
            _reloadGate.Dispose();
        } catch {
        }
    }
}
