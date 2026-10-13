using System.Runtime.InteropServices;

namespace Overlayer.Native;

internal static class O5Native {
    private static readonly bool available = TryLoad();

    public static bool IsAvailable => available;

    private static bool TryLoad() {
        try {
            foreach (string path in CandidatePaths()) {
                try {
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                        continue;
                    }
                    if (Preload(path)) {
                        return true;
                    }
                } catch {
                }
            }
        } catch {
        }
        return false;
    }

    private static IEnumerable<string> CandidatePaths() {
        var paths = new List<string>();
        void Add(string path) {
            try {
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path)) {
                    paths.Add(path);
                }
            } catch {
            }
        }

        try {
            Add(Environment.GetEnvironmentVariable("O5NATIVE_PATH"));
        } catch {
        }

        try {
            string dir = Environment.GetEnvironmentVariable("O5NATIVE_DIR");
            if (!string.IsNullOrEmpty(dir)) {
                Add(Path.Combine(dir, FileName()));
            }
        } catch {
        }

        string self = null;
        try {
            self = typeof(O5Native).Assembly.Location;
            if (string.IsNullOrEmpty(self)) {
                self = new Uri(typeof(O5Native).Assembly.CodeBase).LocalPath;
            }
        } catch {
        }
        if (!string.IsNullOrEmpty(self)) {
            string selfDir = null;
            try {
                selfDir = Path.GetDirectoryName(self);
            } catch {
            }
            if (!string.IsNullOrEmpty(selfDir)) {
                Add(Path.Combine(selfDir, FileName()));
                try {
                    string parent = Path.GetDirectoryName(selfDir);
                    if (!string.IsNullOrEmpty(parent)) {
                        Add(Path.Combine(parent, "UserLibs", FileName()));
                    }
                } catch {
                }
            }
        }

        try {
            string cwd = Directory.GetCurrentDirectory();
            if (!string.IsNullOrEmpty(cwd)) {
                Add(Path.Combine(cwd, "UserLibs", FileName()));
                Add(Path.Combine(cwd, "..", "O5Kit", "native", "zig-out", "lib", FileName()));
                Add(Path.Combine(cwd, "..", "..", "O5Kit", "native", "zig-out", "lib", FileName()));
            }
        } catch {
        }

        try {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir)) {
                Add(Path.Combine(baseDir, "UserLibs", FileName()));
                Add(Path.Combine(baseDir, "..", "..", "..", "..", "..", "O5Kit", "native", "zig-out", "lib", FileName()));
                Add(Path.Combine(baseDir, "..", "..", "..", "O5Kit", "native", "zig-out", "lib", FileName()));
            }
        } catch {
        }

        return paths;
    }

    private static string FileName() {
        try {
            if (Path.DirectorySeparatorChar == '\\') {
                return "O5KitNative.win.dll";
            }
            if (File.Exists("/System/Library/CoreServices/SystemVersion.plist")) {
                return "O5KitNative.mac.dylib";
            }
        } catch {
        }
        return "O5KitNative.linux.so";
    }

    internal static bool Preload(string fullPath) {
        try {
            if (Path.DirectorySeparatorChar == '\\') {
                return LoadLibraryW(fullPath) != IntPtr.Zero;
            }
            const int RTLD_NOW = 2;
            const int RTLD_GLOBAL = 0x100;
            try {
                if (DlopenLibDl2(fullPath, RTLD_NOW | RTLD_GLOBAL) != IntPtr.Zero) {
                    return true;
                }
            } catch {
            }
            try {
                if (DlopenLibDl(fullPath, RTLD_NOW | RTLD_GLOBAL) != IntPtr.Zero) {
                    return true;
                }
            } catch {
            }
        } catch {
        }
        return false;
    }

    [DllImport("libdl.so.2", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr DlopenLibDl2(string fileName, int flags);

    [DllImport("libdl", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr DlopenLibDl(string fileName, int flags);

    [DllImport("kernel32", EntryPoint = "LoadLibraryW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);
}
