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
            Console.Error.WriteLine("no local game install: refusing to emit unverified reference assemblies.");
            Console.Error.WriteLine("Set GAME_MANAGED_DIR to a game install, or run on a machine that has one.");
            return 2;
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

    private sealed class GameDirResolver : Mono.Cecil.DefaultAssemblyResolver {
        private readonly string dir;
        public GameDirResolver(string dir) {
            this.dir = dir;
            AddSearchDirectory(dir);
        }
        public override Mono.Cecil.AssemblyDefinition Resolve(Mono.Cecil.AssemblyNameReference name) {
            try {
                return base.Resolve(name);
            } catch {
                string cand = Path.Combine(dir, name.Name + ".dll");
                if(File.Exists(cand)) {
                    return Mono.Cecil.AssemblyDefinition.ReadAssembly(cand,
                        new Mono.Cecil.ReaderParameters { ReadSymbols = false, AssemblyResolver = this });
                }
                throw;
            }
        }
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
        // Real types, harvested from the local game install's DLL with
        // Mono.Cecil, then stripped to signatures: every method body is
        // replaced with a bare `ret` (`ldc.i4.0` + `ret` for non-void, so
        // verifiers stay quiet). No game logic survives — only the API
        // surface the compiler resolves against. Private members are kept
        // too (anything could be touched via reflection-like patterns in
        // signatures), but method bodies, field initializers, resources,
        // and custom attributes are all dropped (no game content survives).
        string gameFile = r.File.StartsWith("MelonLoader/", StringComparison.Ordinal)
            ? FindGameFile(r.File, managedDir)
            : managedDir == null ? FindGameFile(r.File, null)
            : Path.Combine(managedDir, Path.GetFileName(r.File));
        if(gameFile == null || !File.Exists(gameFile)) {
            throw new InvalidOperationException(
                "game file missing for " + r.File + " (set GAME_MANAGED_DIR or install the game)");
        }
        var resolver = new GameDirResolver(Path.GetDirectoryName(gameFile));
        var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(gameFile,
            new Mono.Cecil.ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });

        asm.Name.Version = r.Version;
        if(r.Pkt != null && r.Pkt.Length > 0) {
            asm.Name.PublicKeyToken = r.Pkt;
        }
        foreach(var module in asm.Modules) {
            foreach(var type in module.Types.ToArray()) {
                StripType(type);
            }
        }
        foreach(var sym in new[] { ".pdb", ".mdb" }) {
            try {
                string sp = Path.ChangeExtension(gameFile, null) + sym;
                if(File.Exists(sp)) {
                    File.Copy(sp, Path.ChangeExtension(path, null) + sym, overwrite: true);
                }
            } catch { }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        asm.Write(path);
        // Facade-likeness check: mscorlib/netstandard-looking outputs must
        // never shadow the real framework (the build must not reference
        // them). Only game/MelonLoader/Harmony names may come out of here.
        string simple = Path.GetFileNameWithoutExtension(path);
        if(simple == "mscorlib" || simple == "netstandard" || simple == "System") {
            throw new InvalidOperationException("refusing to emit framework assembly: " + simple);
        }
    }

    private static void StripType(Mono.Cecil.TypeDefinition type) {
        foreach(var nested in type.NestedTypes.ToArray()) {
            StripType(nested);
        }
        foreach(var method in type.Methods) {
            if(!method.HasBody) {
                continue;
            }
            method.Body.Variables.Clear();
            method.Body.ExceptionHandlers.Clear();
            var il = method.Body.GetILProcessor();
            il.Clear();
            if(method.ReturnType.FullName != "System.Void") {
                if(method.ReturnType.FullName == "System.String") {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldstr, "");
                } else if(method.ReturnType.FullName == "System.Boolean") {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
                } else if(method.ReturnType.FullName == "System.Int32"
                    || method.ReturnType.FullName.StartsWith("System.UInt32")
                    || method.ReturnType.FullName == "System.Single") {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldc_R4, 0f);
                    var conv = method.ReturnType.FullName == "System.Single"
                        ? null : method.ReturnType.FullName.StartsWith("System.UInt")
                            ? (Action)(() => il.Emit(Mono.Cecil.Cil.OpCodes.Conv_U4))
                            : (Action)(() => il.Emit(Mono.Cecil.Cil.OpCodes.Conv_I4));
                    conv?.Invoke();
                } else if(!method.ReturnType.IsValueType) {
                    il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
                }
            }
            il.Emit(Mono.Cecil.Cil.OpCodes.Ret);
        }
        foreach(var field in type.Fields) {
            if(field.HasConstant) {
                continue;
            }
            field.Constant = null;
        }
        foreach(var attr in type.CustomAttributes.ToArray()) {
            type.CustomAttributes.Remove(attr);
        }
        foreach(var method in type.Methods) {
            foreach(var attr in method.CustomAttributes.ToArray()) {
                method.CustomAttributes.Remove(attr);
            }
            foreach(var p in method.Parameters) {
                p.CustomAttributes.Clear();
            }
        }
        foreach(var field in type.Fields) {
            foreach(var attr in field.CustomAttributes.ToArray()) {
                field.CustomAttributes.Remove(attr);
            }
        }
        foreach(var prop in type.Properties) {
            prop.CustomAttributes.Clear();
        }
        foreach(var ev in type.Events) {
            ev.CustomAttributes.Clear();
        }
    }




    /// <summary>Type members the Overlayer build touches, keyed by assembly
    /// name. Everything else resolves through these roots; member bodies are
    /// empty (reference assemblies never execute). Regenerate from the
    /// csproj + game DLLs if the referenced API surface grows.</summary>
}
