#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace KFAssemblyPatcher
{
    public sealed class AssemblyScanInput
    {
        public string Path { get; set; } = string.Empty;
        public string AssemblyName { get; set; } = string.Empty;
        public string Mvid { get; set; } = string.Empty;
        public long Length { get; set; }
        public long LastWriteUtcTicks { get; set; }

        public static AssemblyScanInput FromFile(string path)
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(path))
                return Create(path, assembly.Name.FullName, assembly.MainModule.Mvid.ToString("D"));
        }

        public static AssemblyScanInput Create(string path, string assemblyName, string mvid)
        {
            var file = new FileInfo(System.IO.Path.GetFullPath(path));
            if (!file.Exists) throw new FileNotFoundException("Loaded assembly file is missing.", file.FullName);
            return new AssemblyScanInput { Path = file.FullName, AssemblyName = assemblyName, Mvid = mvid,
                Length = file.Length, LastWriteUtcTicks = file.LastWriteTimeUtc.Ticks };
        }

        public bool Matches(AssemblyScanInput other) => other != null &&
            AssemblyCollectionScanner.PathComparer.Equals(Path, other.Path) && AssemblyName == other.AssemblyName &&
            Mvid == other.Mvid && Length == other.Length && LastWriteUtcTicks == other.LastWriteUtcTicks;

        public void VerifyIdentity(AssemblyDefinition assembly)
        {
            if (AssemblyName != assembly.Name.FullName || Mvid != assembly.MainModule.Mvid.ToString("D"))
                throw new AssemblyPatchException("The assembly file differs from the loaded module: " + Path);
        }
    }

    public sealed class AssemblyScanCache
    {
        public int RulesVersion { get; set; } = AssemblyCollectionScanner.RulesVersion;
        public string RulesBuild { get; set; } = AssemblyCollectionScanner.RulesBuild;
        public AssemblyScanInput Game { get; set; }
        public List<AssemblyScanInput> Entries { get; set; } = new();
    }

    public sealed class AssemblyScanReport
    {
        public List<string> RepairPaths { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Details { get; } = new();
        public AssemblyScanCache Cache { get; } = new();
        public int ScannedAssemblies { get; internal set; }
        public int CacheHits { get; internal set; }
        public bool NeedsRepair => RepairPaths.Count != 0;
    }

    public static class AssemblyCollectionScanner
    {
        public const int RulesVersion = 1;
        public static readonly string RulesBuild = typeof(AssemblyCollectionScanner).Assembly.ManifestModule.ModuleVersionId.ToString("D");
        public static readonly StringComparer PathComparer = System.IO.Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        // The first input is always the actual Assembly-CSharp loaded by this game.
        public static AssemblyScanReport Scan(IReadOnlyList<AssemblyScanInput> inputs, AssemblyScanCache cache)
        {
            if (inputs == null || inputs.Count == 0) throw new ArgumentException("Game assembly input is required.");
            var report = new AssemblyScanReport();
            var gameStamp = AssemblyScanInput.Create(inputs[0].Path, inputs[0].AssemblyName, inputs[0].Mvid);
            report.Cache.Game = gameStamp;
            bool validCache = cache != null && cache.RulesVersion == RulesVersion &&
                cache.RulesBuild == RulesBuild && gameStamp.Matches(cache.Game);
            var previous = new Dictionary<string, AssemblyScanInput>(PathComparer);
            if (validCache && cache.Entries != null)
                foreach (var entry in cache.Entries) if (entry != null && entry.Path != null) previous[entry.Path] = entry;
            // Resolution can change when a dependency changes even if the caller
            // DLL does not. Reuse positives only for an unchanged full collection.
            try
            {
                if (previous.Count != inputs.Select(input => input.Path).Distinct(PathComparer).Count() ||
                    inputs.Any(input => !previous.TryGetValue(input.Path, out var entry) ||
                        !entry.Matches(AssemblyScanInput.Create(input.Path, input.AssemblyName, input.Mvid))))
                    previous.Clear();
            }
            catch { previous.Clear(); }
            using (var resolver = CreateResolver(inputs))
            {
                var loaded = new List<AssemblyDefinition>();
                var seen = new HashSet<string>(PathComparer);
                try
                {
                    for (int index = 0; index < inputs.Count; index++)
                    {
                        var input = inputs[index];
                        try
                        {
                            var stamp = AssemblyScanInput.Create(input.Path, input.AssemblyName, input.Mvid);
                            if (!seen.Add(stamp.Path))
                            {
                                loaded.Add(null);   // 去重项占位，保持下标与 inputs 对齐
                                continue;
                            }
                            if (previous.TryGetValue(stamp.Path, out var cached) && stamp.Matches(cached))
                            {
                                report.CacheHits++;
                                report.Cache.Entries.Add(stamp);
                                loaded.Add(null);
                                continue;
                            }
                            // 缓存命中与否都要读入：某个模块的 override 会决定另一个候选
                            // 的调用点是否可改写，判定不能只看被扫描的那一个程序集。
                            var assembly = Read(stamp.Path, resolver);
                            input.VerifyIdentity(assembly);
                            loaded.Add(assembly);
                        }
                        catch (Exception exception)
                        {
                            report.Errors.Add(input.Path + ": " + exception.Message);
                            loaded.Add(null);
                        }
                    }

                    // loaded[0] 是游戏程序集；其余非空项是模块。
                    var gameAssembly = loaded.Count > 0 ? loaded[0] : null;
                    var modules = loaded.Skip(1).Where(assembly => assembly != null).ToArray();
                    for (int index = 0; index < inputs.Count; index++)
                    {
                        var assembly = loaded[index];
                        if (assembly == null) continue;
                        var input = inputs[index];
                        try
                        {
                            report.ScannedAssemblies++;
                            var result = index == 0
                                ? AssemblyVirtualizer.Analyze(assembly, modules)
                                : AssemblyVirtualizer.AnalyzeMod(assembly, gameAssembly, modules);
                            var stamp = AssemblyScanInput.Create(input.Path, input.AssemblyName, input.Mvid);
                            if (result.Changed)
                            {
                                report.RepairPaths.Add(stamp.Path);
                                report.Details.Add(stamp.Path + ": " + result.TypesChanged +
                                    " sealed types, " + result.MethodsChanged + " method flags, " +
                                    result.CallSitesChanged + " dispatch calls require repair.");
                            }
                            else report.Cache.Entries.Add(stamp);
                        }
                        catch (Exception exception) { report.Errors.Add(input.Path + ": " + exception.Message); }
                    }
                }
                finally
                {
                    foreach (var assembly in loaded) assembly?.Dispose();
                }
            }
            return report;
        }

        internal static DefaultAssemblyResolver CreateResolver(IEnumerable<AssemblyScanInput> inputs)
        {
            return new CatalogResolver(inputs.ToArray());
        }

        private sealed class CatalogResolver : DefaultAssemblyResolver
        {
            private readonly Dictionary<string, AssemblyScanInput[]> manifest;
            private readonly Dictionary<string, AssemblyDefinition> loaded = new(StringComparer.Ordinal);

            internal CatalogResolver(AssemblyScanInput[] inputs)
            {
                manifest = inputs.GroupBy(input => input.AssemblyName, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.GroupBy(input => input.Path, PathComparer)
                        .Select(paths => paths.First()).ToArray(), StringComparer.Ordinal);
                foreach (var directory in inputs.Select(input => System.IO.Path.GetDirectoryName(input.Path)).Distinct(PathComparer))
                    AddSearchDirectory(directory);
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                if (!manifest.TryGetValue(name.FullName, out var matches)) return base.Resolve(name, parameters);
                if (matches.Length != 1)
                    throw new AssemblyPatchException("Loaded assembly identity maps to multiple files: " + name.FullName);
                if (loaded.TryGetValue(name.FullName, out var cached)) return cached;
                var result = Read(matches[0].Path, this);
                try { matches[0].VerifyIdentity(result); }
                catch { result.Dispose(); throw; }
                loaded.Add(name.FullName, result);
                return result;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    foreach (var assembly in loaded.Values) assembly.Dispose();
                    loaded.Clear();
                }
                base.Dispose(disposing);
            }
        }

        internal static AssemblyDefinition Read(string path, IAssemblyResolver resolver) =>
            AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver,
                InMemory = true, ReadSymbols = false, ReadingMode = ReadingMode.Deferred });
    }
}
