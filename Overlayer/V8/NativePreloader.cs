#if !IL2CPP
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Overlayer.V8;

/// <summary>
/// Preloads ClearScript's native V8 library by absolute path before first use.
/// Mono's DllImport probing does not cover UserLibs on old Unity, so the
/// bare-soname pinvokes inside ClearScript would otherwise throw
/// <see cref="DllNotFoundException"/>. Once the image is loaded, later bare-name
/// resolutions hit the already-loaded module. Idempotent and never throws.
/// </summary>
internal static class NativePreloader {
    private const int RTLD_NOW = 0x2;
    private const int RTLD_GLOBAL = 0x100;

    private static bool _done;

    public static void EnsureLoaded() {
        if(_done) {
            return;
        }
        _done = true;
        try {
            string[] files = NativeFileNames();
            if(files == null || files.Length == 0) {
                return;
            }
            foreach(var dir in CandidateDirs()) {
                foreach(var file in files) {
                    try {
                        string full = Path.Combine(dir, file);
                        if(!File.Exists(full)) {
                            continue;
                        }
                        if(LoadNative(full)) {
                            return;
                        }
                    } catch {
                    }
                }
            }
        } catch {
        }
    }

    private static string[] NativeFileNames() {
        try {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return ["ClearScriptV8.win-x64.dll"];
            }
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return ["ClearScriptV8.osx-arm64.dylib", "ClearScriptV8.osx-x64.dylib"];
            }
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return ["ClearScriptV8.linux-x64.so"];
            }
        } catch {
        }
        return null;
    }

    private static string[] CandidateDirs() {
        var dirs = new string[4];
        int n = 0;
        try {
            string self = Assembly.GetExecutingAssembly().Location;
            if(!string.IsNullOrEmpty(self)) {
                string mods = Path.GetDirectoryName(self);
                if(!string.IsNullOrEmpty(mods)) {
                    dirs[n++] = Path.Combine(mods, "..", "UserLibs");
                }
            }
        } catch {
        }
        try {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if(!string.IsNullOrEmpty(baseDir)) {
                dirs[n++] = Path.Combine(baseDir, "UserLibs");
                dirs[n++] = baseDir;
            }
        } catch {
        }
        var result = new string[n];
        Array.Copy(dirs, result, n);
        return result;
    }

    private static bool LoadNative(string fullPath) {
        try {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return LoadLibraryW(fullPath) != IntPtr.Zero;
            }
            if(DlopenInternal(fullPath)) {
                return true;
            }
            if(DlopenLibDl(fullPath)) {
                return true;
            }
            return DlopenLibC(fullPath);
        } catch {
            return false;
        }
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DllImport("__Internal", EntryPoint = "dlopen")]
    private static extern IntPtr DlopenSelf(string path, int flags);

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern IntPtr DlopenLibDl2(string path, int flags);

    [DllImport("libc.so.6", EntryPoint = "dlopen")]
    private static extern IntPtr DlopenLibC6(string path, int flags);

    private static bool DlopenInternal(string fullPath) {
        try {
            return DlopenSelf(fullPath, RTLD_NOW | RTLD_GLOBAL) != IntPtr.Zero;
        } catch {
            return false;
        }
    }

    private static bool DlopenLibDl(string fullPath) {
        try {
            return DlopenLibDl2(fullPath, RTLD_NOW | RTLD_GLOBAL) != IntPtr.Zero;
        } catch {
            return false;
        }
    }

    private static bool DlopenLibC(string fullPath) {
        try {
            return DlopenLibC6(fullPath, RTLD_NOW | RTLD_GLOBAL) != IntPtr.Zero;
        } catch {
            return false;
        }
    }
}
#endif
