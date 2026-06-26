using Microsoft.ClearScript.V8;
using Overlayer.Tag.Core;
using Overlayer.V8.Scripting.Diagnostic;
using System.Security.Cryptography;

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
            await Task.Run(() => {
                lock(_syncLock) {
                    Diagnostics.Clear();
                    var files = Directory.GetFiles(folderPath, "*.js");
                    var currentFiles = new HashSet<string>(files);

                    var removedFiles = _fileHashes.Keys.Where(f => !currentFiles.Contains(f)).ToList();
                    foreach(var file in removedFiles) {
                        UnloadScript(file);
                    }

                    foreach(var file in files) {
                        string currentHash = GetFileHash(file);
                        if(_fileHashes.TryGetValue(file, out var existingHash) && existingHash == currentHash) {
                            continue;
                        }

                        UnloadScript(file);
                        LoadScript(file, currentHash, engine);
                    }
                }
            });

            return true;
        } finally {
            _debounceLock.Release();
        }
    }

    public void LoadScript(string filePath, string hash, V8ScriptEngine engine) {
        UnloadScript(filePath);

        var host = new JSTagRegistrationHost(this, filePath);
        engine.AddHostType(nameof(TagType), typeof(TagType));
        engine.AddHostObject(nameof(JSTagRegistrationHost.RegisterTag), (Action<string, object, object>)host.RegisterTag);

        try {
            engine.Execute(File.ReadAllText(filePath));
            _fileHashes[filePath] = hash;
        } catch(Exception e) {
            Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.ScriptError, JSSeverity.Error, filePath, e));
        }
    }

    private void UnloadScript(string filePath) {
        if(_fileToTags.TryGetValue(filePath, out var tags)) {
            if(tags != null && tags.Count > 0) {
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