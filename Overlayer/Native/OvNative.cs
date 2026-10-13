using System.Runtime.InteropServices;

namespace Overlayer.Native;

internal static class OvNative {
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
            Add(Environment.GetEnvironmentVariable("OVERLAYER_NATIVE_PATH"));
        } catch {
        }

        try {
            string dir = Environment.GetEnvironmentVariable("OVERLAYER_NATIVE_DIR");
            if (!string.IsNullOrEmpty(dir)) {
                Add(Path.Combine(dir, FileName()));
            }
        } catch {
        }

        string self = null;
        try {
            self = typeof(OvNative).Assembly.Location;
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
                Add(Path.Combine(cwd, "native", "zig-out", "lib", FileName()));
                Add(Path.Combine(cwd, "..", "native", "zig-out", "lib", FileName()));
                Add(Path.Combine(cwd, "..", "..", "native", "zig-out", "lib", FileName()));
            }
        } catch {
        }

        try {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir)) {
                Add(Path.Combine(baseDir, "UserLibs", FileName()));
                Add(Path.Combine(baseDir, "native", "zig-out", "lib", FileName()));
                Add(Path.Combine(baseDir, "..", "..", "..", "..", "native", "zig-out", "lib", FileName()));
            }
        } catch {
        }

        return paths;
    }

    private static string FileName() {
        try {
            if (Path.DirectorySeparatorChar == '\\') {
                return "OverlayerNative.win.dll";
            }
            if (File.Exists("/System/Library/CoreServices/SystemVersion.plist")) {
                return "OverlayerNative.mac.dylib";
            }
        } catch {
        }
        return "OverlayerNative.linux.so";
    }

    private static bool Preload(string fullPath) => O5Native.Preload(fullPath);
}
