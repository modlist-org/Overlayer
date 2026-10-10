// Produces the CI reference assemblies checked in at tool/build-refs/game/.
//
// We cannot ship the game's copyrighted DLLs in the repo. Instead this tool
// harvests ONLY their API surface (type/member signatures) with Mono.Cecil,
// replaces every method body with `ret` / `throw null`, and drops all managed
// resources (I18N tables, etc.). No game logic or game data survives — just
// enough metadata for the C# compiler to resolve the csproj HintPaths.
// Custom attributes, const values and signatures are kept byte-identical so
// overload resolution, `params`, optional args and extension methods behave
// exactly like they do against the real game.
//
// Modes:
//   GenRefs describe <dll>                 prints "Name, Version=x[, pkt=HEX]"
//   GenRefs <txt> <sha256> <outdir>        verify hashes vs local game install,
//                                          then emit stripped refs under
//                                          <outdir>/game/... (needs the game)
//   GenRefs verify <txt> <stubroot>        check checked-in stubs at
//                                          <stubroot>/game/... match <txt>
//                                          (no game needed; used by CI)
//
// Layout under <outdir>/<stubroot> mirrors the game: game/GameData/Managed/
// *.dll + game/MelonLoader/net35/{0Harmony,MelonLoader}.dll
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

    private static List<Ref> ReadRefs(string path) {
        var refs = new List<Ref>();
        foreach(string line in File.ReadAllLines(path)) {
            string t = line.Trim();
            if(t.Length == 0 || t.StartsWith('#')) {
                continue;
            }
            // file|AssemblyName, Version=x.y.z.w[, pkt=HEX]
            string[] parts = t.Split('|');
            if(parts.Length != 2) {
                throw new InvalidOperationException("bad line: " + t);
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
                throw new InvalidOperationException("bad line: " + t);
            }
            refs.Add(new Ref { File = file, Name = name, Version = version, Pkt = pkt });
        }
        return refs;
    }

    private static int Main(string[] args) {
        if(args.Length == 3 && args[0] == "retarget") {
            var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[1]);
            foreach(var r in asm.MainModule.AssemblyReferences) {
                if(r.Name == "netstandard") {
                    r.Version = new Version(2, 0, 0, 0);
                }
            }
            asm.Write(args[2]);
            Console.WriteLine($"Wrote retargeted assembly to {args[2]}");
            return 0;
        }
        if(args.Length >= 2 && args[0] == "check") {
            string target = args[1];
            var searchDirs = new List<string>();
            for(int i = 2; i < args.Length; i++) {
                if(Directory.Exists(args[i])) {
                    searchDirs.Add(args[i]);
                }
            }
            string targetDir = Path.GetDirectoryName(Path.GetFullPath(target));
            if(!searchDirs.Contains(targetDir)) searchDirs.Add(targetDir);

            var resolver = new InspectionResolver(searchDirs);
            var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(target,
                new Mono.Cecil.ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });

            int unresolvedTypes = 0;
            int unresolvedMethods = 0;
            int unresolvedFields = 0;
            int totalTypes = 0;
            int totalMethods = 0;
            int totalFields = 0;

            foreach(var tr in asm.MainModule.GetTypeReferences()) {
                totalTypes++;
                try {
                    var td = tr.Resolve();
                    if(td == null) {
                        Console.WriteLine($"[UNRESOLVED TYPE] {tr.FullName} (Scope: {tr.Scope})");
                        unresolvedTypes++;
                    }
                } catch(Exception ex) {
                    Console.WriteLine($"[ERROR RESOLVING TYPE] {tr.FullName}: {ex.Message}");
                    unresolvedTypes++;
                }
            }

            foreach(var mr in asm.MainModule.GetMemberReferences()) {
                if(mr is Mono.Cecil.MethodReference meth) {
                    if(meth.DeclaringType is Mono.Cecil.ArrayType || meth.DeclaringType is Mono.Cecil.GenericParameter) {
                        continue;
                    }
                    totalMethods++;
                    try {
                        var md = meth.Resolve();
                        if(md == null) {
                            Console.WriteLine($"[UNRESOLVED METHOD] {meth.DeclaringType.FullName}::{meth.Name} (Scope: {meth.DeclaringType.Scope})");
                            unresolvedMethods++;
                        }
                    } catch(Exception ex) {
                        Console.WriteLine($"[ERROR RESOLVING METHOD] {meth.FullName}: {ex.Message}");
                        unresolvedMethods++;
                    }
                } else if(mr is Mono.Cecil.FieldReference fld) {
                    if(fld.DeclaringType is Mono.Cecil.ArrayType || fld.DeclaringType is Mono.Cecil.GenericParameter) {
                        continue;
                    }
                    totalFields++;
                    try {
                        var fd = fld.Resolve();
                        if(fd == null) {
                            Console.WriteLine($"[UNRESOLVED FIELD] {fld.DeclaringType.FullName}::{fld.Name} (Scope: {fld.DeclaringType.Scope})");
                            unresolvedFields++;
                        }
                    } catch(Exception ex) {
                        Console.WriteLine($"[ERROR RESOLVING FIELD] {fld.FullName}: {ex.Message}");
                        unresolvedFields++;
                    }
                }
            }

            Console.WriteLine($"=== Resolution Results for {Path.GetFileName(target)} ===");
            Console.WriteLine($"Types: {totalTypes - unresolvedTypes}/{totalTypes} resolved, {unresolvedTypes} unresolved");
            Console.WriteLine($"Methods: {totalMethods - unresolvedMethods}/{totalMethods} resolved, {unresolvedMethods} unresolved");
            Console.WriteLine($"Fields: {totalFields - unresolvedFields}/{totalFields} resolved, {unresolvedFields} unresolved");

            if(unresolvedTypes > 0 || unresolvedMethods > 0 || unresolvedFields > 0) {
                return 1;
            }
            return 0;
        }
        if(args.Length == 2 && args[0] == "dump") {
            try {
                var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[1],
                    new Mono.Cecil.ReaderParameters { ReadSymbols = false });
                Console.WriteLine($"Assembly: {asm.Name.FullName}");
                foreach(var r in asm.MainModule.AssemblyReferences) {
                    Console.WriteLine($"  AssemblyRef: {r.FullName}");
                }
                foreach(var tr in asm.MainModule.GetTypeReferences()) {
                    Console.WriteLine($"  TypeRef: {tr.FullName} (Scope: {tr.Scope})");
                }
                foreach(var t in asm.MainModule.Types) {
                    Console.WriteLine($"  Type: {t.FullName}");
                    foreach(var m in t.Methods) {
                        Console.WriteLine($"    Method: {m.FullName}");
                    }
                }
                return 0;
            } catch(Exception e) {
                Console.Error.WriteLine("dump failed: " + e.Message);
                return 2;
            }
        }
        if(args.Length == 2 && args[0] == "describe") {
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
        if(args.Length == 3 && args[0] == "verify") {
            // CI identity check: stubs on disk must match the checked-in list.
            List<Ref> refs;
            try {
                refs = ReadRefs(args[1]);
            } catch(Exception e) {
                Console.Error.WriteLine(e.Message);
                return 2;
            }
            foreach(var r in refs) {
                string stub = r.File.StartsWith("MelonLoader/", StringComparison.Ordinal)
                    ? Path.Combine(args[2], "game/MelonLoader/net35", Path.GetFileName(r.File))
                    : Path.Combine(args[2], "game/GameData/Managed", Path.GetFileName(r.File));
                if(!File.Exists(stub)) {
                    Console.Error.WriteLine("stub missing: " + stub);
                    return 2;
                }
                var n = Mono.Cecil.AssemblyDefinition.ReadAssembly(stub,
                    new Mono.Cecil.ReaderParameters { ReadSymbols = false }).Name;
                string pkt = n.PublicKeyToken is byte[] t && t.Length > 0
                    ? BitConverter.ToString(t).Replace("-", "")
                    : null;
                string wantPkt = r.Pkt != null && r.Pkt.Length > 0
                    ? BitConverter.ToString(r.Pkt).Replace("-", "")
                    : null;
                if(n.Name != r.Name || n.Version != r.Version || pkt != wantPkt) {
                    Console.Error.WriteLine($"identity mismatch for {r.File}: " +
                        $"stub is {n.Name}, Version={n.Version}" +
                        (pkt == null ? "" : ", pkt=" + pkt));
                    return 2;
                }
            }
            Console.WriteLine($"verified {refs.Count} stub identities");
            return 0;
        }
        if(args.Length != 3) {
            Console.Error.WriteLine("usage: GenRefs <build-refs.txt> <build-refs.sha256> <outdir>");
            Console.Error.WriteLine("   or: GenRefs describe <dll>");
            Console.Error.WriteLine("   or: GenRefs verify <build-refs.txt> <stubroot>");
            return 2;
        }

        List<Ref> emitRefs;
        try {
            emitRefs = ReadRefs(args[0]);
        } catch(Exception e) {
            Console.Error.WriteLine(e.Message);
            return 2;
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
        if(managed == null) {
            Console.Error.WriteLine("no local game install: set GAME_MANAGED_DIR or install the game.");
            return 2;
        }
        Console.WriteLine("verifying against local game install: " + managed);
        using(var sha = SHA256.Create()) {
            foreach(var r in emitRefs) {
                string gameFile = FindGameFile(r.File, managed);
                if(string.IsNullOrEmpty(gameFile) || !File.Exists(gameFile)) {
                    Console.Error.WriteLine("game file missing: " + (gameFile ?? r.File));
                    return 2;
                }
                string hex = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(gameFile))).ToLowerInvariant();
                if(!hashes.TryGetValue(r.File, out string want) || want != hex) {
                    Console.Error.WriteLine($"hash mismatch for {r.File}: got {hex}, want {want ?? "<none>"}");
                    return 2;
                }
            }
        }
        Console.WriteLine($"verified {emitRefs.Count} hashes against local game install");

        foreach(var r in emitRefs) {
            string dir = r.File.StartsWith("MelonLoader/", StringComparison.Ordinal)
                ? Path.Combine(args[2], "game/MelonLoader/net35")
                : Path.Combine(args[2], "game/GameData/Managed");
            Directory.CreateDirectory(dir);
            EmitReference(Path.Combine(dir, Path.GetFileName(r.File)), r, managed);
        }
        Console.WriteLine($"wrote {emitRefs.Count} reference assemblies");
        return 0;
    }

    private static string FindGameFile(string file, string managedDir) {
        if(file.StartsWith("MelonLoader/", StringComparison.Ordinal)) {
            foreach(string root in GameRoots(managedDir)) {
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
        return null;
    }

    private static IEnumerable<string> GameRoots(string managedDir) {
        // managedDir = <game>/ADanceOfFireAndIce_Data/Managed -> up 2 = <game>.
        if(managedDir != null) {
            string root = managedDir;
            for(int i = 0; i < 2 && !string.IsNullOrEmpty(root); i++) {
                root = Path.GetDirectoryName(root);
            }
            if(!string.IsNullOrEmpty(root)) {
                yield return root;
            }
        }
        string env = Environment.GetEnvironmentVariable("ADOFAI_DIR");
        if(!string.IsNullOrEmpty(env)) {
            yield return env;
        }
        yield return Path.Combine(HomeDir(), ".local/share/Steam/steamapps/common/A Dance of Fire and Ice");
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
        // Harvest the real API surface, then strip everything executable:
        // every method body becomes `ret` (void) or `throw null` (works for
        // ANY return type, like real reference assemblies), and all managed
        // resources are dropped. Signatures, attributes, constants and the
        // pinned identity are kept, so compilation behaves identically.
        string gameFile = FindGameFile(r.File, managedDir);
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
            foreach(var res in module.Resources.ToArray()) {
                module.Resources.Remove(res);
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        asm.Write(path);
        string simple = Path.GetFileNameWithoutExtension(path);
        if(simple == "mscorlib" || simple == "netstandard" || simple == "System") {
            throw new InvalidOperationException("refusing to emit framework assembly: " + simple);
        }
    }

    private static void StripType(Mono.Cecil.TypeDefinition type) {
        foreach(var nested in type.NestedTypes.ToArray()) {
            StripType(nested);
        }
        foreach(var method in type.Methods.ToArray()) {
            if(!method.HasBody) {
                continue;
            }
            if(method.IsPInvokeImpl || method.IsAbstract || method.IsRuntime) {
                continue;
            }
            StripBody(method);
        }
        foreach(var field in type.Fields) {
            if(field.HasConstant) {
                continue;
            }
            field.Constant = null;
        }
    }

    private static void StripBody(Mono.Cecil.MethodDefinition method) {
        var body = new Mono.Cecil.Cil.MethodBody(method);
        body.MaxStackSize = 8;
        method.Body = body;
        var il = body.GetILProcessor();
        if(method.ReturnType.FullName == "System.Void") {
            il.Emit(Mono.Cecil.Cil.OpCodes.Ret);
        } else {
            il.Emit(Mono.Cecil.Cil.OpCodes.Ldnull);
            il.Emit(Mono.Cecil.Cil.OpCodes.Throw);
        }
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

    private sealed class InspectionResolver : Mono.Cecil.DefaultAssemblyResolver {
        private readonly List<string> dirs;
        private readonly Dictionary<string, Mono.Cecil.AssemblyDefinition> cache = new(StringComparer.OrdinalIgnoreCase);

        public InspectionResolver(IEnumerable<string> dirs) {
            this.dirs = new List<string>(dirs);
            foreach(var d in this.dirs) {
                AddSearchDirectory(d);
            }
        }

        public override Mono.Cecil.AssemblyDefinition Resolve(Mono.Cecil.AssemblyNameReference name) {
            if(cache.TryGetValue(name.Name, out var cached)) {
                return cached;
            }
            try {
                var def = base.Resolve(name);
                cache[name.Name] = def;
                return def;
            } catch {
                foreach(var dir in dirs) {
                    string cand = Path.Combine(dir, name.Name + ".dll");
                    if(File.Exists(cand)) {
                        var def = Mono.Cecil.AssemblyDefinition.ReadAssembly(cand,
                            new Mono.Cecil.ReaderParameters { ReadSymbols = false, AssemblyResolver = this });
                        cache[name.Name] = def;
                        return def;
                    }
                }
                throw;
            }
        }
    }
}
