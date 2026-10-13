using Microsoft.ClearScript.V8;
using Overlayer.Async;
using Overlayer.Compat.Interface;
using Overlayer.Core;
using static Overlayer.Overlay.OvObject;
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
    private readonly SemaphoreSlim _disabledScriptsSaveGate = new(1, 1);
    private readonly object _toggleRebuildSync = new();
    private Task _toggleRebuildTask;
    private long _toggleRevision;
    public IReadOnlyList<JSDiagnostic> LoaderDiagnostics => _scriptLoader.Diagnostics;

    public IReadOnlyList<string> ScriptFiles => _scriptLoader.GetScriptFiles();

    public IReadOnlyList<string> ScriptFileTags(string filePath) => _scriptLoader.GetFileTags(filePath);

    public Task InitializationTask { get; private set; }
    private FileSystemWatcher _watcher;

    /// <summary>False when the V8 native library could not be loaded (JS disabled, rest of the mod keeps running).</summary>
    public bool IsAvailable { get; private set; }
    public string UnavailableReason { get; private set; }

    public const string ImplFileName = "impl.js";
    public const string ImplDtsFileName = "impl.d.ts";
    public const string ScriptFolderName = "Script";
    private const string DisabledScriptsFileName = "DisabledScripts.json";
    private const string PrefsFileName = "Prefs.json";
    private PrefsStore _prefs;

    public string ScriptFolderPath { get; private set; }
    public string ImplFilePath { get; private set; }
    public string ImplDtsFilePath { get; private set; }

    public V8ScriptEngine Engine {
        get {
            lock (_engineLock) {
                return _engine;
            }
        }
    }

    public void Initialize() => InitializationTask = InitializeAsync();

    /// <summary>Never throws: a missing V8 native library disables JS instead of aborting mod init.</summary>
    public Task InitializeAsync() {
        ImplFilePath = Path.Combine(MainCore.Paths.JSPath, ImplFileName);
        ImplDtsFilePath = Path.Combine(MainCore.Paths.JSPath, ImplDtsFileName);
        ScriptFolderPath = Path.Combine(MainCore.Paths.JSPath, ScriptFolderName);

        if (!Directory.Exists(ScriptFolderPath)) {
            Directory.CreateDirectory(ScriptFolderPath);
            MainCore.Log.Msg($"[{nameof(V8Manager)}] Script folder created at: {ScriptFolderPath}");
        }
        lock (_engineLock) {
            _engine = TryCreateEngineLocked();
        }

        return LoadDisabledScriptsAsync();
    }

    /// <summary>Creates + binds an engine. Caller must hold <see cref="_engineLock"/>. Returns null on failure.</summary>
    private V8ScriptEngine TryCreateEngineLocked() {
        try {
            ClearScriptNativeLoader.EnsureLoaded(
                msg => MainCore.Log.Msg(msg),
                warn => MainCore.Log.Wrn(warn));
        } catch (Exception ex) {
            MainCore.Log.Wrn($"[{nameof(V8Manager)}] Native preload failed: {ex.Message}");
        }
        try {
            var engine = new V8ScriptEngine();
            BindEngine(engine);
            IsAvailable = true;
            UnavailableReason = null;
            return engine;
        } catch (DllNotFoundException ex) {
            IsAvailable = false;
            UnavailableReason = ex.Message;
            MainCore.Log.Wrn($"[{nameof(V8Manager)}] V8 unavailable (native library not resolved: {ex.Message}). JS tags/scripts are disabled; the rest of Overlayer keeps running.");
            MainCore.Log.Msg($"[{nameof(V8Manager)}] Fix: reinstall the matching per-OS zip (linux/mac zips must contain ClearScriptV8.linux-x64.so / .dylib in UserLibs) and restart the game once so it can be staged next to the executable.");
            return null;
        } catch (Exception ex) {
            IsAvailable = false;
            UnavailableReason = ex.Message;
            MainCore.Log.Wrn($"[{nameof(V8Manager)}] V8 unavailable: {ex.GetType().Name}: {ex.Message}. JS tags/scripts are disabled; the rest of Overlayer keeps running.");
            return null;
        }
    }

    public async Task LoadScriptsAsync() {
        await _reloadGate.WaitAsync().ConfigureAwait(false);
        try {
            try {
                await InitializationTask.ConfigureAwait(false);
                if (_engine == null) {
                    MainCore.Log.Wrn($"[{nameof(V8Manager)}] Engine unavailable, skipping script load.");
                    return;
                }
                if (await _scriptLoader.LoadAllScriptsAsync(ScriptFolderPath, _engine, syncChanges: false).ConfigureAwait(false)) {
                    foreach (var diag in LoaderDiagnostics) {
                        MainCore.Log.Msg(diag.ToString());
                    }
                }
            } catch (Exception e) {
                MainCore.Log.Err($"[{nameof(V8Manager)}] Script load failed: {e.Message}");
            }
            await Task.Run(() => GenerateImplJs(force: true)).ConfigureAwait(false);
            MainThread.Enqueue(TextEngineUpdater.RecompileAll);
        } finally {
            _reloadGate.Release();
        }
        UpdateWatcher();
    }

    private void BindEngine(V8ScriptEngine engine) {
        engine.AddHostObject(nameof(TagAccessHelper), new TagAccessHelper());
        engine.AddHostObject(nameof(Store), Store);
        engine.AddHostObject("Prefs", _prefs ??= new PrefsStore(Path.Combine(ScriptFolderPath, PrefsFileName)));
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
        engine.AddHostType("O5Input", typeof(O5Kit.Input.O5Input));
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
        lock (_engineLock) {
            if (_engine == null) {
                return null;
            }
            return fn.Invoke(false, callArgs);
        }
    }

    public void Reset() {
        lock (_engineLock) {
            ClearFxScriptCache();
            try {
                _engine?.Dispose();
            } catch {
            }
            _engine = TryCreateEngineLocked();
            if (_engine == null) {
                return;
            }
        }
        Scripting.Patch.JSPatchManager.RemoveAll();
        Scripting.UI.JSUIHost.RemoveAll();
        LoadImplJs();
    }

    public async Task ReloadScriptsAsync() {
        await _reloadGate.WaitAsync();
        try {
            var preview = await Task.Run(() => _scriptLoader.PreviewChanges(ScriptFolderPath));
            if (preview.changed.Count == 0 && preview.removed.Count == 0) {
                return;
            }
            await RebuildScriptsUnderGateAsync();
        } finally {
            _reloadGate.Release();
        }
    }

    /// <summary>Disposes the engine and re-executes every enabled script. Call with <see cref="_reloadGate"/> held.</summary>
    private async Task RebuildScriptsUnderGateAsync() {
        await InitializationTask.ConfigureAwait(false);
        await Task.Run(() => {
            Scripting.Patch.JSPatchManager.RemoveAll();
            Scripting.UI.JSUIHost.RemoveAll();
            TagCache.Instance.Clear();

            List<string> jsTags;
            lock (_engineLock) {
                ClearFxScriptCache();
                jsTags = JSTagManager.Clear();
                try {
                    _engine?.Dispose();
                } catch {
                }
                _engine = TryCreateEngineLocked();
            }

            if (jsTags.Count > 0) {
                TagManager.Unregister([.. jsTags], recompile: false);
            }
            _scriptLoader.ResetTracking();

            if (_engine == null) {
                MainCore.Log.Wrn($"[{nameof(V8Manager)}] Engine unavailable, skipping script rebuild.");
                return;
            }
        }).ConfigureAwait(false);

        if (await _scriptLoader.LoadAllScriptsAsync(ScriptFolderPath, _engine, syncChanges: false).ConfigureAwait(false)) {
            foreach (var diag in LoaderDiagnostics) {
                MainCore.Log.Msg(diag.ToString());
            }
        }
        await Task.Run(() => GenerateImplJs(false)).ConfigureAwait(false);
        MainThread.Enqueue(TextEngineUpdater.RecompileAll);
    }

    /// <summary>Forces a full rebuild regardless of file changes (e.g. after toggling a script on/off).</summary>
    public async Task RebuildScriptsAsync() {
        await _reloadGate.WaitAsync();
        try {
            await RebuildScriptsUnderGateAsync();
        } finally {
            _reloadGate.Release();
        }
    }

    public void GenerateImplJs(bool force = false) {
        var tags = TagManager.GetAllTags();
        var jsContent = BuildImplJsContent(tags);
        var dtsContent = BuildImplDtsContent(tags);

        lock (_engineLock) {
            if (_engine == null) {
                return;
            }
            try {
                bool jsUpToDate = !force && File.Exists(ImplFilePath) &&
                    File.ReadAllText(ImplFilePath) == jsContent;
                if (jsUpToDate) {
                    _engine.Execute(jsContent);
                } else {
                    File.WriteAllText(ImplFilePath, jsContent);
                    _engine.Execute(jsContent);
                    MainCore.Log.Msg($"[{nameof(V8Manager)}] {ImplFileName} changed, synced and loaded.");
                }

                if (!string.IsNullOrEmpty(ImplDtsFilePath)) {
                    bool dtsUpToDate = !force && File.Exists(ImplDtsFilePath) &&
                        File.ReadAllText(ImplDtsFilePath) == dtsContent;
                    if (!dtsUpToDate) {
                        File.WriteAllText(ImplDtsFilePath, dtsContent);
                        MainCore.Log.Msg($"[{nameof(V8Manager)}] {ImplDtsFileName} changed, synced.");
                    }
                }
            } catch (Exception ex) {
                MainCore.Log.Err($"[{nameof(V8Manager)}] Failed to sync {ImplFileName}: {ex.Message}");
            }
        }
    }

    private string BuildImplJsContent(IEnumerable<TagCore> tags) => ImplJsGenerator.BuildJs(tags);

    private string BuildImplDtsContent(IEnumerable<TagCore> tags) => ImplJsGenerator.BuildDts(tags);

    private readonly Dictionary<string, (V8ScriptEngine Engine, V8Script Script, string Error)> _fxScriptCache = [];
    private readonly Dictionary<string, string> _fxRuntimeErrors = [];
    private readonly Dictionary<string, long> _fxTimeoutBackoff = [];
    private long _evalSeq;
    private long _evalActive = -1;

    private (V8Script Script, string Error) CompileFx(string code) {
        if (_engine == null) {
            return (null, "V8 engine unavailable");
        }
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

        lock (_engineLock) {
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

        lock (_engineLock) {
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
            if (!task.IsCanceled && Interlocked.CompareExchange(ref running, 0, 1) == 1
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
        } catch (Exception ex) {
            result = null;
            if (timedOut) {
                RecordTimeout(code);
            } else {
                RecordRuntimeError(code, ex.Message);
            }

            return false;
        } finally {
            timeout.Cancel();
            Interlocked.Exchange(ref running, 0);
            if (Volatile.Read(ref _evalActive) == seq) {
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

        lock (_engineLock) {
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
        foreach (var entry in _fxScriptCache.Values) {
            try {
                entry.Script?.Dispose();
            } catch {
            }
        }

        _fxScriptCache.Clear();
        _fxRuntimeErrors.Clear();
        _fxTimeoutBackoff.Clear();
    }

    public void LoadImplJs() {
        lock (_engineLock) {
            if (_engine == null) {
                return;
            }
            if (File.Exists(ImplFilePath)) {
                try {
                    _engine.Execute(File.ReadAllText(ImplFilePath));
                } catch (Exception ex) {
                    MainCore.Log.Wrn($"[{nameof(V8Manager)}] Load failed: {ex.Message}");
                }
            }
        }
    }

    public void ReloadScriptFile(string filePath) {
        _ = ReloadScriptFileAsync(filePath);
    }

    public void UnloadScriptFile(string filePath) {
        _ = UnloadScriptFileAsync(filePath);
    }

    public async Task UnloadScriptFileAsync(string filePath) {
        await InitializationTask.ConfigureAwait(false);
        await _reloadGate.WaitAsync().ConfigureAwait(false);
        try {
            await Task.Run(() => _scriptLoader.UnloadScriptFile(filePath)).ConfigureAwait(false);
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(V8Manager)}] Script unload failed: {e.Message}");
        } finally {
            _reloadGate.Release();
        }
    }

    public async Task ReloadScriptFileAsync(string filePath) {
        await InitializationTask.ConfigureAwait(false);
        await _reloadGate.WaitAsync().ConfigureAwait(false);
        try {
            if (_engine == null) {
                MainCore.Log.Wrn($"[{nameof(V8Manager)}] Engine unavailable, skipping script reload.");
                return;
            }
            await Task.Run(() => _scriptLoader.ReloadFile(filePath, _engine)).ConfigureAwait(false);
            foreach (var diag in LoaderDiagnostics) {
                MainCore.Log.Msg(diag.ToString());
            }
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(V8Manager)}] Script reload failed: {e.Message}");
        } finally {
            _reloadGate.Release();
        }
    }

    public bool IsScriptEnabled(string filePath) {
        try {
            return !_scriptLoader.IsFileDisabled(JSScriptLoader.RelativeName(ScriptFolderPath, filePath));
        } catch {
            return true;
        }
    }

    public async Task SetScriptEnabledAsync(string filePath, bool enabled) {
        await InitializationTask.ConfigureAwait(false);
        string name;
        try {
            name = JSScriptLoader.RelativeName(ScriptFolderPath, filePath);
        } catch {
            return;
        }
        if (!_scriptLoader.SetFileDisabled(name, !enabled)) {
            return;
        }
        Task saveTask = SaveDisabledScriptsAsync();
        Task rebuildTask = RequestToggleRebuildAsync();
        await Task.WhenAll(saveTask, rebuildTask).ConfigureAwait(false);
    }

    private async Task LoadDisabledScriptsAsync() {
        _scriptLoader.SetDisabledFileNames([]);
        try {
            string path = Path.Combine(ScriptFolderPath, DisabledScriptsFileName);
            if (!File.Exists(path)) {
                return;
            }
            string json = await Task.Run(() => File.ReadAllText(path)).ConfigureAwait(false);
            var names = Newtonsoft.Json.JsonConvert.DeserializeObject<HashSet<string>>(json);
            if (names == null) {
                return;
            }
            _scriptLoader.SetDisabledFileNames(names);
            if (await Task.Run(PruneOrphanDisabledScripts).ConfigureAwait(false)) {
                await SaveDisabledScriptsAsync().ConfigureAwait(false);
            }
        } catch (Exception e) {
            MainCore.Log.Wrn($"[{nameof(V8Manager)}] Failed to load disabled scripts: {e.Message}");
        }
    }

    private bool PruneOrphanDisabledScripts() {
        HashSet<string> diskFiles;
        try {
            diskFiles = new HashSet<string>(
                JSScriptLoader.FindScripts(ScriptFolderPath).Select(file => JSScriptLoader.RelativeName(ScriptFolderPath, file)),
                StringComparer.OrdinalIgnoreCase);
        } catch {
            return false;
        }
        bool removed = false;
        foreach (string name in _scriptLoader.GetDisabledFileNames()) {
            if (!diskFiles.Contains(name)) {
                _scriptLoader.SetFileDisabled(name, false);
                removed = true;
            }
        }
        return removed;
    }

    private async Task SaveDisabledScriptsAsync() {
        await _disabledScriptsSaveGate.WaitAsync().ConfigureAwait(false);
        try {
            string path = Path.Combine(ScriptFolderPath, DisabledScriptsFileName);
            string content = SerializeDisabledScripts();
            await Task.Run(() => File.WriteAllText(path, content)).ConfigureAwait(false);
        } catch (Exception e) {
            MainCore.Log.Wrn($"[{nameof(V8Manager)}] Failed to save disabled scripts: {e.Message}");
        } finally {
            _disabledScriptsSaveGate.Release();
        }
    }

    private string SerializeDisabledScripts() => Newtonsoft.Json.JsonConvert.SerializeObject(
        _scriptLoader.GetDisabledFileNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
        Newtonsoft.Json.Formatting.Indented);

    private Task RequestToggleRebuildAsync() {
        lock (_toggleRebuildSync) {
            _toggleRevision++;
            return _toggleRebuildTask ??= RebuildToggledScriptsAsync();
        }
    }

    private async Task RebuildToggledScriptsAsync() {
        try {
            while (true) {
                // Coalesce rapid switches into one engine rebuild.
                await Task.Delay(100).ConfigureAwait(false);
                long revision;
                lock (_toggleRebuildSync) {
                    revision = _toggleRevision;
                }

                await RebuildScriptsAsync().ConfigureAwait(false);

                lock (_toggleRebuildSync) {
                    if (revision == _toggleRevision) {
                        _toggleRebuildTask = null;
                        return;
                    }
                }
            }
        } catch {
            lock (_toggleRebuildSync) {
                _toggleRebuildTask = null;
            }
            throw;
        }
    }

    public void UpdateWatcher() {
        bool enabled = MainCore.Conf.EnableJSScriptWatcher;
        if (enabled && _watcher == null) {
            _watcher = new FileSystemWatcher(ScriptFolderPath, "*.js") {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true
            };
            _watcher.Changed += OnScriptChanged;
            _watcher.Created += OnScriptChanged;
            _watcher.Deleted += OnScriptChanged;
            _watcher.Renamed += OnScriptChanged;
            _watcher.EnableRaisingEvents = true;
        } else if (!enabled && _watcher != null) {
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
        _ = DebouncedReloadAsync(cts.Token);
    }

    private async Task DebouncedReloadAsync(CancellationToken cancellationToken) {
        try {
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            await ReloadScriptsAsync().ConfigureAwait(false);
        } catch (OperationCanceledException) {
        } catch (Exception e) {
            MainCore.Log.Err($"[{nameof(V8Manager)}] Script reload failed: {e.Message}");
        }
    }

    public void Dispose() {
        lock (_engineLock) {
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
