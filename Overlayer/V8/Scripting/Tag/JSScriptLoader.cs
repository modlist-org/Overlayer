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

    public async Task<bool> LoadAllScriptsAsync(string folderPath, V8ScriptEngine engine) {
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
                        string currentHash = GetFileHash(file);
                        if(_fileHashes.TryGetValue(file, out var existingHash) && existingHash == currentHash) {
                            continue;
                        }

                        UnloadScript(file);
                        LoadScriptInternal(file, currentHash, engine);
                        hasChanges = true;
                    }

                    if(hasChanges) {
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
        engine.AddHostType(nameof(TagType), typeof(TagType));
        engine.AddHostObject(
            JSTagRegistrationHost.HostBindingName,
            (Action<string, object, object, string>)host.RegisterTag
        );

        try {
            engine.Execute(JSTagRegistrationHost.BindingScript);
            string source = JSScriptPreprocessor.RemoveImplImports(
                File.ReadAllText(filePath)
            );
            engine.Execute(source);
            _fileHashes[filePath] = hash;
        } catch(Exception e) {
            Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.ScriptError, JSSeverity.Error, filePath, e));
        }
    }

    private static void SyncV8AndRecompile() {
        MainCore.V8.GenerateImplJs();
        MainCore.V8.LoadImplJs();
        MainThread.Enqueue(TextEngineUpdater.RecompileAll);
    }

    private void UnloadScript(string filePath) {
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

    private static string GetFileHash(string filePath) {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return BitConverter.ToString(sha256.ComputeHash(stream));
    }
}
