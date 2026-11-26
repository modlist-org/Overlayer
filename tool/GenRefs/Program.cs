// Produces the CI reference assemblies checked in at tool/build-refs/{mono,il2cpp}/.
//
// Harvests ONLY the API surface (type/member signatures) from the sibling O5Dev
// Unity host with Mono.Cecil, replaces every method body with `ret` / `throw null`,
// and drops all managed resources. Signatures, attributes, constants and pinned
// identities are kept so compilation behaves identically to building against O5Dev.
//
// Modes:
//   GenRefs describe <dll>                 prints "Name, Version=x[, pkt=HEX]"
//   GenRefs <txt> <sha256> <outdir>        verify hashes vs O5Dev/Builds/{Mono,IL2CPP},
//                                          then emit stripped refs under <outdir>/<relpath>
//   GenRefs verify <txt> <stubroot>        check checked-in stubs at <stubroot>/<relpath>
//                                          match <txt> (no O5Dev needed; used by CI)
//
// Layout under <outdir>/<stubroot>:
//   mono/MelonLoader/net35/{0Harmony,MelonLoader,Newtonsoft.Json}.dll
//   mono/O5Dev_Data/Managed/*.dll
//   il2cpp/MelonLoader/net6/{0Harmony,MelonLoader,Il2CppInterop.Runtime,Newtonsoft.Json}.dll
//   il2cpp/MelonLoader/Il2CppAssemblies/*.dll
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
            RetargetNetstandard20(asm);
            asm.Write(args[2]);
            Console.WriteLine($"Wrote retargeted assembly to {args[2]}");
            return 0;
        }
        if(args.Length == 3 && args[0] == "retarget-dir") {
            string srcDir = args[1];
            string dstDir = args[2];
            if(!Directory.Exists(srcDir)) {
                Console.Error.WriteLine("missing source dir: " + srcDir);
                return 2;
            }
            Directory.CreateDirectory(dstDir);
            int rewritten = 0;
            int copied = 0;
            foreach(string src in Directory.GetFiles(srcDir, "*.dll")) {
                string name = Path.GetFileName(src);
                string dst = Path.Combine(dstDir, name);
                string simple = Path.GetFileNameWithoutExtension(src);
                if(simple == "mscorlib" || simple == "netstandard" || simple == "System" || simple.StartsWith("System.", StringComparison.Ordinal)) {
                    File.Copy(src, dst, true);
                    copied++;
                    continue;
                }
                try {
                    var resolver = new GameDirResolver(srcDir);
                    using var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(src,
                        new Mono.Cecil.ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });
                    if(RetargetNetstandard20(asm)) {
                        asm.Write(dst);
                        rewritten++;
                    } else {
                        File.Copy(src, dst, true);
                        copied++;
                    }
                } catch {
                    File.Copy(src, dst, true);
                    copied++;
                }
            }
            Console.WriteLine($"retarget-dir: {rewritten} retargeted, {copied} copied -> {dstDir}");
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
                string stub = ResolveStubPath(args[2], r.File);
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

        string buildsDir = Environment.GetEnvironmentVariable("O5DEV_BUILDS_DIR");
        string managed = Environment.GetEnvironmentVariable("GAME_MANAGED_DIR");
        if(string.IsNullOrEmpty(buildsDir) && string.IsNullOrEmpty(managed)) {
            buildsDir = FindO5DevBuildsDir();
        }
        if(string.IsNullOrEmpty(buildsDir) && string.IsNullOrEmpty(managed)) {
            Console.Error.WriteLine("no local O5Dev builds: set O5DEV_BUILDS_DIR or build O5Dev.");
            return 2;
        }
        Console.WriteLine("verifying against local source: " + (buildsDir ?? managed));
        using(var sha = SHA256.Create()) {
            foreach(var r in emitRefs) {
                string srcFile = FindSourceFile(r.File, buildsDir, managed);
                if(string.IsNullOrEmpty(srcFile) || !File.Exists(srcFile)) {
                    Console.Error.WriteLine("source file missing: " + (srcFile ?? r.File));
                    return 2;
                }
                string hex = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(srcFile))).ToLowerInvariant();
                if(!hashes.TryGetValue(r.File, out string want) || want != hex) {
                    Console.Error.WriteLine($"hash mismatch for {r.File}: got {hex}, want {want ?? "<none>"}");
                    return 2;
                }
            }
        }
        Console.WriteLine($"verified {emitRefs.Count} hashes against local source");

        foreach(var r in emitRefs) {
            string dest = ResolveStubPath(args[2], r.File);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            EmitReference(dest, r, buildsDir, managed);
        }
        Console.WriteLine($"wrote {emitRefs.Count} reference assemblies");
        return 0;
    }

    private static string ResolveStubPath(string root, string relFile) {
        if(relFile.StartsWith("mono/", StringComparison.Ordinal) || relFile.StartsWith("il2cpp/", StringComparison.Ordinal)) {
            return Path.Combine(root, relFile);
        }
        return relFile.StartsWith("MelonLoader/", StringComparison.Ordinal)
            ? Path.Combine(root, "game/MelonLoader/net35", Path.GetFileName(relFile))
            : Path.Combine(root, "game/GameData/Managed", Path.GetFileName(relFile));
    }

    private static string FindO5DevBuildsDir() {
        string dir = Directory.GetCurrentDirectory();
        for(int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++) {
            string cand = Path.Combine(dir, "O5Dev", "Builds");
            if(Directory.Exists(cand)) {
                return cand;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string FindSourceFile(string file, string buildsDir, string managedDir) {
        if(!string.IsNullOrEmpty(buildsDir)) {
            if(file.StartsWith("mono/", StringComparison.Ordinal)) {
                string cand = Path.Combine(buildsDir, "Mono", file.Substring("mono/".Length));
                if(File.Exists(cand)) {
                    return cand;
                }
            } else if(file.StartsWith("il2cpp/", StringComparison.Ordinal)) {
                string cand = Path.Combine(buildsDir, "IL2CPP", file.Substring("il2cpp/".Length));
                if(File.Exists(cand)) {
                    return cand;
                }
            }
        }
        if(file.StartsWith("MelonLoader/", StringComparison.Ordinal) && !string.IsNullOrEmpty(managedDir)) {
            string root = Path.GetDirectoryName(Path.GetDirectoryName(managedDir));
            if(!string.IsNullOrEmpty(root)) {
                string cand = Path.Combine(root, file);
                if(File.Exists(cand)) {
                    return cand;
                }
            }
        }
        if(!string.IsNullOrEmpty(managedDir)) {
            string cand = Path.Combine(managedDir, Path.GetFileName(file));
            if(File.Exists(cand)) {
                return cand;
            }
        }
        return null;
    }

    private static void EmitReference(string path, Ref r, string buildsDir, string managedDir) {
        // Harvest the real API surface, then strip everything executable:
        // every method body becomes `ret` (void) or `throw null` (works for
        // ANY return type, like real reference assemblies), and all managed
        // resources are dropped. Signatures, attributes, constants and the
        // pinned identity are kept, so compilation behaves identically.
        string gameFile = FindSourceFile(r.File, buildsDir, managedDir);
        if(gameFile == null || !File.Exists(gameFile)) {
            throw new InvalidOperationException(
                "source file missing for " + r.File + " (set O5DEV_BUILDS_DIR or build O5Dev)");
        }
        var resolver = new GameDirResolver(Path.GetDirectoryName(gameFile));
        var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(gameFile,
            new Mono.Cecil.ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });
        asm.Name.Version = r.Version;
        if(r.Pkt != null && r.Pkt.Length > 0) {
            asm.Name.PublicKeyToken = r.Pkt;
        }
        if(r.File.StartsWith("mono/", StringComparison.Ordinal)) {
            RetargetNetstandard20(asm);
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

    private static bool RetargetNetstandard20(Mono.Cecil.AssemblyDefinition asm) {
        bool changed = false;
        foreach(var module in asm.Modules) {
            Mono.Cecil.AssemblyNameReference nsRef = null;
            foreach(var ar in module.AssemblyReferences) {
                if(ar.Name == "netstandard" && (ar.Version.Major > 2 || (ar.Version.Major == 2 && ar.Version.Minor > 0))) {
                    ar.Version = new Version(2, 0, 0, 0);
                    nsRef = ar;
                    changed = true;
                }
            }
            if(nsRef != null) {
                Mono.Cecil.AssemblyNameReference memRef = null;
                foreach(var tr in module.GetTypeReferences()) {
                    if(tr.Scope == nsRef && (tr.FullName == "System.ReadOnlySpan`1" || tr.FullName == "System.Span`1"
                        || tr.FullName == "System.ReadOnlyMemory`1" || tr.FullName == "System.Memory`1")) {
                        if(memRef == null) {
                            memRef = new Mono.Cecil.AssemblyNameReference("System.Memory", new Version(4, 0, 1, 2)) {
                                PublicKeyToken = new byte[] { 0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51 }
                            };
                            module.AssemblyReferences.Add(memRef);
                        }
                        tr.Scope = memRef;
                    }
                }
            }
        }
        return changed;
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
