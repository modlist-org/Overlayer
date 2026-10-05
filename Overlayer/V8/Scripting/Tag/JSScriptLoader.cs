using Microsoft.ClearScript.V8;
using Overlayer.Async;
using Overlayer.Core;
using Overlayer.Tag.Core;
using Overlayer.V8.Scripting.Diagnostic;
using System.Security.Cryptography;
using static Overlayer.Overlay.OvObject;

namespace Overlayer.V8.Scripting.Tag;

public class JSScriptLoader {
    private readonly object _syncLock = new();
    private readonly SemaphoreSlim _debounceLock = new(1, 1);

    public List<JSDiagnostic> Diagnostics { get; } = [];
    private readonly Dictionary<string, string> _fileHashes = [];
    private readonly Dictionary<string, List<string>> _fileToTags = [];

    /// <summary>Script file names (not full paths) skipped by the loader. Guarded by <see cref="_syncLock"/>; use the helpers below.</summary>
    private readonly HashSet<string> _disabledFileNames = new(StringComparer.OrdinalIgnoreCase);

    public bool IsFileDisabled(string fileName) {
        lock(_syncLock) {
            return _disabledFileNames.Contains(fileName);
        }
    }

    /// <summary>Returns true when the set changed.</summary>
    public bool SetFileDisabled(string fileName, bool disabled) {
        lock(_syncLock) {
            return disabled ? _disabledFileNames.Add(fileName) : _disabledFileNames.Remove(fileName);
        }
    }

    public List<string> GetDisabledFileNames() {
        lock(_syncLock) {
            return [.. _disabledFileNames];
        }
    }

    public void SetDisabledFileNames(IEnumerable<string> names) {
        lock(_syncLock) {
            _disabledFileNames.Clear();
            foreach(string name in names) {
                if(!string.IsNullOrWhiteSpace(name)) {
                    _disabledFileNames.Add(name.Trim());
                }
            }
        }
    }

    public async Task<bool> LoadAllScriptsAsync(string folderPath, V8ScriptEngine engine, bool syncChanges = true) {
        if(!await _debounceLock.WaitAsync(0)) {
            return false;
        }

        try {
            bool hasChanges = false;
            await Task.Run(() => {
                lock(_syncLock) {
                    Diagnostics.Clear();
                    var files = Directory.GetFiles(folderPath, "*.js");
                    var currentFiles = new HashSet<string>(files);

                    var removedFiles = _fileHashes.Keys.Where(f => !currentFiles.Contains(f)).ToList();
                    foreach(var file in removedFiles) {
                        UnloadScript(file);
                        hasChanges = true;
                    }

                    foreach(var file in files) {
                        if(IsDisabled(file)) {
                            if(_fileHashes.ContainsKey(file)) {
                                UnloadScript(file);
                                hasChanges = true;
                            }
                            continue;
                        }
                        string currentHash = TryGetFileHash(file);
                        if(currentHash == null) {
                            continue;
                        }
                        if(_fileHashes.TryGetValue(file, out var existingHash) && existingHash == currentHash) {
                            continue;
                        }

                        UnloadScript(file);
                        LoadScriptInternal(file, currentHash, engine);
                        hasChanges = true;
                    }

                    if(hasChanges && syncChanges) {
                        SyncV8AndRecompile();
                    }
                }
            });

            return true;
        } finally {
            _debounceLock.Release();
        }
    }

    public void LoadScript(string filePath, string hash, V8ScriptEngine engine) {
        lock(_syncLock) {
            UnloadScript(filePath);
            LoadScriptInternal(filePath, hash, engine);
            
            SyncV8AndRecompile();
        }
    }

    private void LoadScriptInternal(string filePath, string hash, V8ScriptEngine engine) {
        var host = new JSTagRegistrationHost(this, filePath);
        engine.AddHostObject(
            JSTagRegistrationHost.HostBindingName,
            (Action<string, object, object, string>)host.RegisterTag
        );

        try {
            engine.Execute(JSTagRegistrationHost.BindingScript);
            var patchHost = new Scripting.Patch.JSPatchHost(this, filePath);
            engine.AddHostObject(
                Scripting.Patch.JSPatchHost.HostBindingName,
                (Func<object, object, string, string, int>)patchHost.AddPatch
            );
            engine.AddHostObject(
                "__OverlayerRemovePatch",
                (Func<object, bool>)patchHost.RemovePatch
            );
            engine.Execute(Scripting.Patch.JSPatchHost.BindingScript);
            string source = ReadScriptSource(filePath);
            if(source == null) {
                Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.ScriptError, JSSeverity.Error, filePath,
                    new IOException($"Could not read '{Path.GetFileName(filePath)}' (locked by editor?)")));
                return;
            }
            string processed = JSScriptPreprocessor.RemoveImplImports(source);
            engine.Execute(processed);
            _fileHashes[filePath] = hash;
        } catch(Exception e) {
            Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.ScriptError, JSSeverity.Error, filePath, e));
        }
    }

    private static string ReadScriptSource(string filePath) {
        for(int i = 0; i < 10; i++) {
            try {
                return File.ReadAllText(filePath);
            } catch(IOException) {
                Thread.Sleep(50);
            }
        }
        return null;
    }

    private static void SyncV8AndRecompile() {
        MainCore.V8.GenerateImplJs();
        MainCore.V8.LoadImplJs();
        MainThread.Enqueue(TextEngineUpdater.RecompileAll);
    }

    private void UnloadScript(string filePath) {
        Scripting.Patch.JSPatchManager.RemoveFile(filePath);
        if(_fileToTags.TryGetValue(filePath, out var tags)) {
            if(tags != null && tags.Count > 0) {
                foreach(var tag in tags) {
                    JSTagManager.Remove(tag);
                }
                TagManager.Unregister([.. tags]);
            }
            _fileToTags.Remove(filePath);
        }
        _fileHashes.Remove(filePath);
    }

    public void RegisterFileTag(string filePath, string tagName) {
        if(string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(tagName)) {
            return;
        }

        lock(_syncLock) {
            if(!_fileToTags.TryGetValue(filePath, out var tags)) {
                tags = [];
                _fileToTags[filePath] = tags;
            }

            if(!tags.Contains(tagName)) {
                tags.Add(tagName);
            }
        }
    }

    public (List<string> changed, List<string> removed) PreviewChanges(string folderPath) {
        var changed = new List<string>();
        var removed = new List<string>();
        lock(_syncLock) {
            string[] files;
            try {
                files = Directory.GetFiles(folderPath, "*.js");
            } catch {
                return (changed, removed);
            }
            var current = new HashSet<string>(files);
            foreach(var tracked in _fileHashes.Keys) {
                if(!current.Contains(tracked)) {
                    removed.Add(tracked);
                }
            }
            foreach(var file in files) {
                if(IsDisabled(file)) {
                    continue;
                }
                string hash = TryGetFileHash(file);
                if(hash == null || !_fileHashes.TryGetValue(file, out var existing) || existing != hash) {
                    changed.Add(file);
                }
            }
        }
        return (changed, removed);
    }

    public IReadOnlyList<string> GetScriptFiles() {
        lock(_syncLock) {
            return _fileHashes.Keys.OrderBy(f => f).ToList();
        }
    }

    public IReadOnlyList<string> GetFileTags(string filePath) {
        lock(_syncLock) {
            return _fileToTags.TryGetValue(filePath, out var tags) ? [.. tags] : [];
        }
    }

    /// <summary>Clears all load tracking. The next load re-executes every file (used after an engine rebuild).</summary>
    public void ResetTracking() {
        lock(_syncLock) {
            _fileHashes.Clear();
            _fileToTags.Clear();
        }
    }

    public void ReloadFile(string filePath, V8ScriptEngine engine) {
        lock(_syncLock) {
            if(IsDisabled(filePath)) {
                if(_fileHashes.ContainsKey(filePath)) {
                    UnloadScript(filePath);
                    SyncV8AndRecompile();
                }
                return;
            }
            string hash = TryGetFileHash(filePath);
            if(hash == null) {
                return;
            }
            UnloadScript(filePath);
            LoadScriptInternal(filePath, hash, engine);
            SyncV8AndRecompile();
        }
    }

    public void UnloadScriptFile(string filePath) {
        lock(_syncLock) {
            if(!_fileHashes.ContainsKey(filePath)) {
                return;
            }
            UnloadScript(filePath);
            SyncV8AndRecompile();
        }
    }

    private bool IsDisabled(string filePath) {
        try {
            return IsFileDisabled(Path.GetFileName(filePath));
        } catch {
            return false;
        }
    }

    private static string TryGetFileHash(string filePath) {
        try {
            return GetFileHash(filePath);
        } catch {
            return null;
        }
    }

    private static string GetFileHash(string filePath) {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return BitConverter.ToString(sha256.ComputeHash(stream));
    }
}
