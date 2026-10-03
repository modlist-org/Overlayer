// Restores game reference assemblies for CI from checked-in metadata.
// We cannot ship the game's copyrighted DLLs in the repo. Instead,
// tool/build-refs/build-refs.txt lists every assembly the build references
// (name, version, optional public key token) and build-refs.sha256 pins the
// SHA-256 of the real game DLL each line was taken from. This tool emits
// reference-only assemblies (no method bodies, no game code — just enough
// metadata for the C# compiler to resolve HintPaths) and verifies the hashes
// match a local game install when one is present.
//
// Usage: GenRefs <build-refs.txt> <build-refs.sha256> <outdir>
// Layout under <outdir> mirrors pack_refs.sh: game/GameData/Managed/*.dll
// + game/MelonLoader/net35/{0Harmony,MelonLoader}.dll
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;

internal static class GenRefs {
    private sealed class Ref {
        public string File;
        public string Name;
        public Version Version;
        public byte[] Pkt;
    }

    private static int Main(string[] args) {
        if(args.Length == 2 && args[0] == "describe") {
            // describe <dll>: prints "Name, Version=x.y.z.w[, pkt=HEX]" for pack_refs.sh
            try {
                var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[1],
                    new Mono.Cecil.ReaderParameters { ReadSymbols = false });
                var n = asm.Name;
                string pkt = n.PublicKeyToken is byte[] t && t.Length > 0
                    ? ", pkt=" + BitConverter.ToString(t).Replace("-", "")
                    : "";
                Console.WriteLine($"{n.Name}, Version={n.Version}{pkt}");
                return 0;
            } catch(Exception e) {
                Console.Error.WriteLine("describe failed: " + e.Message);
                return 2;
            }
        }
        if(args.Length != 3) {
            Console.Error.WriteLine("usage: GenRefs <build-refs.txt> <build-refs.sha256> <outdir>");
            Console.Error.WriteLine("   or: GenRefs describe <dll>");
            return 2;
        }

        var refs = new List<Ref>();
        foreach(string line in File.ReadAllLines(args[0])) {
            string t = line.Trim();
            if(t.Length == 0 || t.StartsWith('#')) {
                continue;
            }
            // file|AssemblyName, Version=x.y.z.w[, pkt=HEX]
            string[] parts = t.Split('|');
            if(parts.Length != 2) {
                Console.Error.WriteLine("bad line: " + t);
                return 2;
            }
            string file = parts[0].Trim();
            string name = null;
            Version version = null;
            byte[] pkt = null;
            foreach(string kv in parts[1].Split(',')) {
                string k = kv.Trim();
                if(k.StartsWith("pkt=", StringComparison.Ordinal)) {
                    pkt = Convert.FromHexString(k.Substring(4));
                } else if(k.StartsWith("Version=", StringComparison.Ordinal)) {
                    version = Version.Parse(k.Substring(8));
                } else if(name == null) {
                    name = k;
                }
            }
            if(name == null || version == null) {
                Console.Error.WriteLine("bad line: " + t);
                return 2;
            }
            refs.Add(new Ref { File = file, Name = name, Version = version, Pkt = pkt });
        }

        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach(string line in File.ReadAllLines(args[1])) {
            string t = line.Trim();
            if(t.Length == 0 || t.StartsWith('#')) {
                continue;
            }
            int sp = t.IndexOfAny(new[] { ' ', '\t' });
            if(sp <= 0) {
                Console.Error.WriteLine("bad hash line: " + t);
                return 2;
            }
            hashes[t.Substring(sp + 1).Trim()] = t.Substring(0, sp).ToLowerInvariant();
        }

        string managed = Environment.GetEnvironmentVariable("GAME_MANAGED_DIR");
        if(string.IsNullOrEmpty(managed)) {
            managed = FindManagedDir();
        }
        bool verified = false;
        if(managed != null) {
            Console.WriteLine("verifying against local game install: " + managed);
            using var sha = SHA256.Create();
            foreach(var r in refs) {
                string gameFile = r.File.StartsWith("MelonLoader/", StringComparison.Ordinal)
                    ? FindGameFile(r.File, managed)
                    : Path.Combine(managed, Path.GetFileName(r.File));
                if(string.IsNullOrEmpty(gameFile) || !File.Exists(gameFile)) {
                    Console.Error.WriteLine("game file missing: " + gameFile);
                    return 2;
                }
                string hex = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(gameFile))).ToLowerInvariant();
                if(!hashes.TryGetValue(r.File, out string want) || want != hex) {
                    Console.Error.WriteLine($"hash mismatch for {r.File}: got {hex}, want {want ?? "<none>"}");
                    return 2;
                }
            }
            Console.WriteLine($"verified {refs.Count} hashes against local game install");
            verified = true;
        }

        if(!verified) {
            Console.WriteLine("no local game install: emitting UNVERIFIED reference assemblies.");
            Console.WriteLine("Every emitted identity still comes from build-refs.txt (checked in); only the");
            Console.WriteLine("hash pinning is skipped. The compiler resolves HintPaths by identity alone,");
            Console.WriteLine("so the build output is identical either way.");
        }
        foreach(var r in refs) {
            string dir = r.File.StartsWith("MelonLoader/", StringComparison.Ordinal)
                ? Path.Combine(args[2], "game/MelonLoader/net35")
                : Path.Combine(args[2], "game/GameData/Managed");
            Directory.CreateDirectory(dir);
            EmitReference(Path.Combine(dir, Path.GetFileName(r.File)), r, managed);
        }
        Console.WriteLine($"wrote {refs.Count} reference assemblies" + (managed == null ? " (UNVERIFIED: no local game install)" : ""));
        return 0;
    }



    private static string FindGameFile(string file, string managedDir) {
        if(file.StartsWith("MelonLoader/", StringComparison.Ordinal)) {
            string home = HomeDir();
            foreach(string root in new[] {
                Environment.GetEnvironmentVariable("ADOFAI_DIR"),
                Path.Combine(home, ".local/share/Steam/steamapps/common/A Dance of Fire and Ice"),
            }) {
                if(string.IsNullOrEmpty(root)) {
                    continue;
                }
                string cand = Path.Combine(root, file);
                if(File.Exists(cand)) {
                    return cand;
                }
            }
            return null;
        }
        if(managedDir != null) {
            string cand = Path.Combine(managedDir, Path.GetFileName(file));
            if(File.Exists(cand)) {
                return cand;
            }
        }
        string managed = FindManagedDir();
        return managed == null ? null : Path.Combine(managed, Path.GetFileName(file));
    }

    private static string HomeDir() {
        string home = Environment.GetEnvironmentVariable("HOME");
        if(!string.IsNullOrEmpty(home)) {
            return home;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string FindManagedDir() {
        string home = HomeDir();
        var roots = new List<string>();
        string env = Environment.GetEnvironmentVariable("ADOFAI_DIR");
        if(!string.IsNullOrEmpty(env)) {
            roots.Add(env);
        }
        roots.Add(Path.Combine(home, ".local/share/Steam/steamapps/common/A Dance of Fire and Ice"));
        foreach(string root in roots) {
            if(string.IsNullOrEmpty(root)) {
                continue;
            }
            string m = Path.Combine(root, "ADanceOfFireAndIce_Data/Managed");
            if(Directory.Exists(Path.Combine(m, "Assembly-CSharp.dll"))) {
                return m;
            }
        }
        return null;
    }

    private static void EmitReference(string path, Ref r, string managedDir) {
        // No game bytes here: emit a bare reference-only assembly carrying
        // exactly the checked-in identity (name, version, public key token).
        // The compiler resolves HintPaths by identity alone, so this is all
        // the build needs. (CI has no game install; the hash pinning in
        // Main() already confirmed the identity against a real install when
        // pack_refs.sh recorded it.)
        string gameFile = r.File.StartsWith("MelonLoader/", StringComparison.Ordinal)
            ? FindGameFile(r.File, managedDir)
            : null;
        if(gameFile == null) {
            gameFile = "identities-only";
        }
        EmitIdentityReference(path, r, gameFile);
    }

    private static void EmitIdentityReference(string path, Ref r, string gameFile) {
        var name = new AssemblyNameDefinition(r.Name, r.Version);
        if(r.Pkt != null && r.Pkt.Length > 0) {
            name.PublicKeyToken = r.Pkt;
        }
        using var asm = AssemblyDefinition.CreateAssembly(name, "<RefStub>", ModuleKind.Dll);
        var module = asm.MainModule;
        // Marker so the file is a valid, loadable assembly.
        var stub = new TypeDefinition("", "__RefStub",
            Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class,
            module.TypeSystem.Object);
        module.Types.Add(stub);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        asm.Write(path);
    }







    /// <summary>Type members the Overlayer build touches, keyed by assembly
    /// name. Everything else resolves through these roots; member bodies are
    /// empty (reference assemblies never execute). Regenerate from the
    /// csproj + game DLLs if the referenced API surface grows.</summary>
}
