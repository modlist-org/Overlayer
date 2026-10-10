using System.Reflection;
using System.Runtime.InteropServices;

namespace Overlayer.V8;

/// <summary>
/// Makes ClearScript's native V8 library discoverable on Mono/IL2CPP installs.
///
/// Root cause of `DllNotFoundException: ClearScriptV8.linux-x64.so` on Linux Mono
/// titles (e.g. Superliminal): the .so ships in `UserLibs/`, but Mono's DllImport
/// search only covers the game base directory, LD_LIBRARY_PATH, etc. — never
/// UserLibs. So the file is present yet unfindable.
///
/// This loader runs before any `new V8ScriptEngine()` and:
///   1. locates the native library under UserLibs (via the ClearScript managed
///      assembly location, the Overlayer assembly location, and MelonLoader's
///      game root when available),
///   2. pre-loads it with dlopen(RTLD_GLOBAL) / LoadLibrary so the subsequent
///      static DllImport resolves even if the file is not on the search path,
///   3. extends LD_LIBRARY_PATH / PATH to include UserLibs, and
///   4. copies the platform's native file next to the game executable (Mono's
///      app-base search dir) when it is missing or outdated, so later launches
///      and plain DllImport probes succeed without dlopen tricks.
///
/// All failures are swallowed (best effort): V8Manager treats a still-missing
/// engine as "JS disabled" instead of aborting mod initialization.
/// </summary>
public static class ClearScriptNativeLoader {
    private static readonly object _sync = new();
    private static bool _loaded;

    private const int RTLD_NOW = 2;
    private const int RTLD_GLOBAL = 0x100;

    private static readonly string[] LinuxLibs = [
        "ClearScriptV8.linux-x64.so",
        "ClearScriptV8.linux-arm64.so",
        "ClearScriptV8.linux-arm.so",
        "libnfd.so",
    ];

    private static readonly string[] MacLibs = [
        "ClearScriptV8.osx-arm64.dylib",
        "ClearScriptV8.osx-x64.dylib",
        "libnfd.dylib",
    ];

    private static readonly string[] WindowsLibs = [
        "ClearScriptV8.win-x64.dll",
        "ClearScriptV8.win-x86.dll",
        "ClearScriptV8.win-arm64.dll",
        "nfd.dll",
    ];

    public static bool EnsureLoaded(Action<string> log = null, Action<string> logWarn = null) {
        lock (_sync) {
            if (_loaded) {
                return true;
            }
            try {
                bool ok = EnsureLoadedCore(log, logWarn);
                _loaded = ok;
                return ok;
            } catch (Exception ex) {
                try {
                    logWarn?.Invoke($"[ClearScriptNativeLoader] preload failed: {ex.Message}");
                } catch {
                }
                return false;
            }
        }
    }

    private static bool EnsureLoadedCore(Action<string> log, Action<string> logWarn) {
        string[] wanted = WantedFiles();
        var userLibsDirs = FindUserLibsDirs();
        if (userLibsDirs.Count == 0) {
            logWarn?.Invoke("[ClearScriptNativeLoader] UserLibs directory not found.");
            return false;
        }

        string gameRoot = FindGameRoot(userLibsDirs);
        bool isWindows = IsWindows();

        bool anyFound = false;
        bool anyLoaded = false;
        foreach (string dir in userLibsDirs.Distinct(StringComparer.OrdinalIgnoreCase)) {
            ExtendLibrarySearchPath(dir, isWindows);
            foreach (string name in wanted) {
                string full = Path.Combine(dir, name);
                if (!File.Exists(full)) {
                    continue;
                }
                anyFound = true;
                if (TryPreload(full, isWindows, logWarn)) {
                    anyLoaded = true;
                }
                if (gameRoot != null) {
                    EnsureDiscoverable(full, gameRoot, log, logWarn);
                }
            }
        }

        if (!anyFound) {
            logWarn?.Invoke(
                $"[ClearScriptNativeLoader] No native V8 library found in: {string.Join(", ", userLibsDirs)}. " +
                $"Wanted: {string.Join(", ", wanted)}.");
            return false;
        }

        if (!anyLoaded && gameRoot != null) {
            // dlopen may have failed but the game-root copy can still satisfy
            // Mono's plain DllImport probe on the next engine creation.
            log?.Invoke("[ClearScriptNativeLoader] Preload did not confirm a handle; relying on game-root copy.");
            return true;
        }
        return anyLoaded || anyFound;
    }

    private static string[] WantedFiles() {
        try {
            // Unity platform is the most reliable discriminator (also correct
            // for Proton/Wine titles, which report as WindowsPlayer on a Linux host).
            var platform = UnityEngine.Application.platform;
            switch (platform) {
                case UnityEngine.RuntimePlatform.WindowsPlayer:
                case UnityEngine.RuntimePlatform.WindowsEditor:
                    return WindowsLibs;
                case UnityEngine.RuntimePlatform.OSXPlayer:
                case UnityEngine.RuntimePlatform.OSXEditor:
                    return MacLibs;
                case UnityEngine.RuntimePlatform.LinuxPlayer:
                case UnityEngine.RuntimePlatform.LinuxEditor:
                    return LinuxLibs;
            }
        } catch {
        }
        if (IsWindows()) {
            return WindowsLibs;
        }
        return IsMac() ? MacLibs : LinuxLibs;
    }

    private static bool IsWindows() {
        try {
            var platform = Environment.OSVersion.Platform;
            return platform == PlatformID.Win32NT || platform == PlatformID.Win32S ||
                platform == PlatformID.Win32Windows || platform == PlatformID.WinCE;
        } catch {
            return Path.DirectorySeparatorChar == '\\';
        }
    }

    private static bool IsMac() {
        // Mono reports macOS as Unix; distinguish via well-known system paths.
        try {
            if (File.Exists("/System/Library/CoreServices/SystemVersion.plist")) {
                return true;
            }
            if (Directory.Exists("/System/Library/CoreServices")) {
                return true;
            }
        } catch {
        }
        return false;
    }

    private static List<string> FindUserLibsDirs() {
        var dirs = new List<string>();

        void AddDir(string dir) {
            if (string.IsNullOrEmpty(dir)) {
                return;
            }
            try {
                if (Directory.Exists(dir) && !dirs.Contains(dir, StringComparer.OrdinalIgnoreCase)) {
                    dirs.Add(dir);
                }
            } catch {
            }
        }

        void AddUserLibsUnder(string baseDir) {
            if (string.IsNullOrEmpty(baseDir)) {
                return;
            }
            try {
                AddDir(Path.Combine(baseDir, "UserLibs"));
            } catch {
            }
        }

        // 1. Next to the ClearScript managed assembly (it is loaded from UserLibs).
        try {
            string asmPath = typeof(Microsoft.ClearScript.V8.V8ScriptEngine).Assembly.Location;
            if (!string.IsNullOrEmpty(asmPath)) {
                AddDir(Path.GetDirectoryName(asmPath));
            }
        } catch {
        }

        // 2. MelonLoader game root (reflection: no compile-time dependency).
        try {
            AddUserLibsUnder(GetMelonGameRoot());
        } catch {
        }

        // 3. Relative to the Overlayer assembly (Mods/ -> ../UserLibs).
        try {
            string self = typeof(ClearScriptNativeLoader).Assembly.Location;
            if (string.IsNullOrEmpty(self)) {
                try {
                    self = new Uri(typeof(ClearScriptNativeLoader).Assembly.CodeBase).LocalPath;
                } catch {
                    self = null;
                }
            }
            if (!string.IsNullOrEmpty(self)) {
                string modsDir = Path.GetDirectoryName(self);
                if (!string.IsNullOrEmpty(modsDir)) {
                    AddUserLibsUnder(Path.GetDirectoryName(modsDir));
                    AddUserLibsUnder(modsDir);
                }
            }
        } catch {
        }

        // 4. Process-level fallbacks.
        try {
            AddUserLibsUnder(AppDomain.CurrentDomain.BaseDirectory);
        } catch {
        }
        try {
            AddUserLibsUnder(Directory.GetCurrentDirectory());
        } catch {
        }

        return dirs;
    }

    private static string FindGameRoot(List<string> userLibsDirs) {
        // Prefer MelonLoader's authoritative root.
        try {
            string melonRoot = GetMelonGameRoot();
            if (!string.IsNullOrEmpty(melonRoot) && Directory.Exists(melonRoot)) {
                return melonRoot;
            }
        } catch {
        }
        // Otherwise the parent of any discovered UserLibs dir is the game root.
        foreach (string dir in userLibsDirs) {
            try {
                if (dir.EndsWith("UserLibs", StringComparison.OrdinalIgnoreCase)) {
                    string parent = Path.GetDirectoryName(dir);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)) {
                        return parent;
                    }
                }
            } catch {
            }
        }
        try {
            string cwd = Directory.GetCurrentDirectory();
            if (!string.IsNullOrEmpty(cwd) && Directory.Exists(cwd)) {
                return cwd;
            }
        } catch {
        }
        return null;
    }

    private static string GetMelonGameRoot() {
        try {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => {
                    try {
                        return string.Equals(a.GetName().Name, "MelonLoader", StringComparison.OrdinalIgnoreCase);
                    } catch {
                        return false;
                    }
                });
            Type envType = asm?.GetType("MelonLoader.Utils.MelonEnvironment");
            envType ??= Type.GetType("MelonLoader.Utils.MelonEnvironment, MelonLoader");
            var prop = envType?.GetProperty("GameRootDirectory", BindingFlags.Public | BindingFlags.Static);
            if (prop != null) {
                string root = prop.GetValue(null) as string;
                if (!string.IsNullOrEmpty(root)) {
                    return root;
                }
            }
        } catch {
        }
        return null;
    }

    private static void ExtendLibrarySearchPath(string dir, bool isWindows) {
        try {
            if (isWindows) {
                string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                if (path.IndexOf(dir, StringComparison.OrdinalIgnoreCase) < 0) {
                    Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + path);
                }
                try {
                    SetDllDirectoryW(dir);
                } catch {
                }
            } else {
                foreach (string var in new[] { "LD_LIBRARY_PATH", "DYLD_LIBRARY_PATH" }) {
                    try {
                        string cur = Environment.GetEnvironmentVariable(var) ?? string.Empty;
                        if (cur.IndexOf(dir, StringComparison.Ordinal) < 0) {
                            Environment.SetEnvironmentVariable(var,
                                string.IsNullOrEmpty(cur) ? dir : dir + Path.PathSeparator + cur);
                        }
                    } catch {
                    }
                }
            }
        } catch {
        }
    }

    private static bool TryPreload(string fullPath, bool isWindows, Action<string> logWarn) {
        try {
            if (isWindows) {
                IntPtr h = LoadLibraryW(fullPath);
                if (h != IntPtr.Zero) {
                    return true;
                }
            } else {
                IntPtr h = DlopenAny(fullPath);
                if (h != IntPtr.Zero) {
                    return true;
                }
            }
        } catch (Exception ex) {
            try {
                logWarn?.Invoke($"[ClearScriptNativeLoader] preload {Path.GetFileName(fullPath)} failed: {ex.Message}");
            } catch {
            }
            return false;
        }
        return false;
    }

    private static IntPtr DlopenAny(string fullPath) {
        // Try each libdl provider; missing providers throw DllNotFoundException,
        // a zero handle means the target itself failed to load.
        foreach (Func<string, int, IntPtr> fn in new Func<string, int, IntPtr>[] {
            DlopenLibDl2, DlopenLibDlSo, DlopenLibDl, DlopenInternal,
        }) {
            try {
                IntPtr h = fn(fullPath, RTLD_NOW | RTLD_GLOBAL);
                if (h != IntPtr.Zero) {
                    return h;
                }
            } catch (DllNotFoundException) {
            } catch (EntryPointNotFoundException) {
            } catch {
                return IntPtr.Zero;
            }
        }
        return IntPtr.Zero;
    }

    private static void EnsureDiscoverable(string sourcePath, string gameRoot, Action<string> log, Action<string> logWarn) {
        try {
            string dest = Path.Combine(gameRoot, Path.GetFileName(sourcePath));
            if (string.Equals(
                    Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(dest).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            var src = new FileInfo(sourcePath);
            var dst = new FileInfo(dest);
            bool copy = true;
            try {
                if (dst.Exists && dst.Length == src.Length &&
                    dst.LastWriteTimeUtc == src.LastWriteTimeUtc) {
                    copy = false;
                }
            } catch {
                copy = true;
            }
            if (!copy) {
                return;
            }
            File.Copy(sourcePath, dest, true);
            try {
                File.SetLastWriteTimeUtc(dest, src.LastWriteTimeUtc);
            } catch {
            }
            log?.Invoke($"[ClearScriptNativeLoader] Copied {dst.Name} next to the game executable so Mono can resolve it.");
        } catch (Exception ex) {
            try {
                logWarn?.Invoke($"[ClearScriptNativeLoader] Could not stage {Path.GetFileName(sourcePath)} in game root: {ex.Message}");
            } catch {
            }
        }
    }

    [DllImport("libdl.so.2", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr DlopenLibDl2(string fileName, int flags);

    [DllImport("libdl.so", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr DlopenLibDlSo(string fileName, int flags);

    [DllImport("libdl", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr DlopenLibDl(string fileName, int flags);

    [DllImport("__Internal", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr DlopenInternal(string fileName, int flags);

    [DllImport("kernel32", EntryPoint = "LoadLibraryW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    [DllImport("kernel32", EntryPoint = "SetDllDirectoryW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectoryW(string lpPathName);
}
