using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using KFAssemblyPatcher;
using Newtonsoft.Json;

namespace KFCommonUtilityLib
{
    internal sealed class LoadedAssemblyScan
    {
        internal AssemblyScanInput[] Inputs;
        internal AssemblyScanReport Report;
    }

    internal static class LoadedAssemblyCatalog
    {
        internal static LoadedAssemblyScan Scan(string gamePath, Assembly gameAssembly, bool forceGameScan)
        {
            var timer = Stopwatch.StartNew();
            string cachePath = Path.Combine(GameIO.GetUserGameDataDir(), "KFCommonUtilityLib", "AssemblyCompatibility.json");
            var cache = ReadCache(cachePath);
            if (forceGameScan && cache.Entries != null)
                cache.Entries.RemoveAll(entry => entry != null && AssemblyCollectionScanner.PathComparer.Equals(entry.Path, gamePath));
            var inputs = new List<AssemblyScanInput> { FromLoaded(gamePath, gameAssembly) };
            var errors = new List<string>();
            var seen = new HashSet<string>(AssemblyCollectionScanner.PathComparer) { Path.GetFullPath(gamePath) };
            foreach (var assembly in ModManager.GetLoadedAssemblies().Distinct())
            {
                try
                {
                    string path = ResolveLoadedPath(assembly, cache);
                    if (seen.Add(path)) inputs.Add(FromLoaded(path, assembly));
                }
                catch (Exception exception) { errors.Add(assembly.FullName + ": " + exception.Message); }
            }
            var report = AssemblyCollectionScanner.Scan(inputs, cache);
            report.Errors.AddRange(errors);
            if (report.Errors.Count == 0) SaveCache(cachePath, report.Cache);
            Log.Out("[KFCommonUtilityLib] Assembly compatibility: " + inputs.Count + " files, " +
                report.ScannedAssemblies + " scanned, " + report.CacheHits + " cached, " +
                report.RepairPaths.Count + " repairs, " + timer.ElapsedMilliseconds + " ms.");
            return new LoadedAssemblyScan { Inputs = inputs.ToArray(), Report = report };
        }

        private static AssemblyScanInput FromLoaded(string path, Assembly assembly) =>
            AssemblyScanInput.Create(path, assembly.FullName, assembly.ManifestModule.ModuleVersionId.ToString("D"));

        private static string ResolveLoadedPath(Assembly assembly, AssemblyScanCache cache)
        {
            if (assembly.IsDynamic)
                throw new InvalidOperationException("A dynamic assembly has no file to repair.");
            if (!string.IsNullOrEmpty(assembly.Location))
                return Path.GetFullPath(assembly.Location);

            // Windows dedicated servers use Assembly.Load(byte[]), whose Location
            // is empty. Match the loaded identity and MVID inside its owning mod.
            var owner = ModManager.GetModForAssembly(assembly);
            if (owner == null || string.IsNullOrEmpty(owner.Path))
                throw new InvalidOperationException("Cannot locate the owning mod for an assembly loaded from bytes.");
            string directory = Path.GetFullPath(owner.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string mvid = assembly.ManifestModule.ModuleVersionId.ToString("D");
            var cached = (cache.Entries ?? new List<AssemblyScanInput>()).Where(entry => entry != null &&
                entry.AssemblyName == assembly.FullName && entry.Mvid == mvid &&
                AssemblyCollectionScanner.PathComparer.Equals(Path.GetDirectoryName(entry.Path), directory)).ToArray();
            if (cached.Length == 1 && File.Exists(cached[0].Path) &&
                cached[0].Matches(FromLoaded(cached[0].Path, assembly)))
                return cached[0].Path;
            var matches = new List<string>();
            foreach (string path in Directory.GetFiles(directory).Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var candidate = AssemblyScanInput.FromFile(path);
                    if (candidate.AssemblyName == assembly.FullName && candidate.Mvid == mvid)
                        matches.Add(candidate.Path);
                }
                catch (BadImageFormatException) { }
            }
            if (matches.Count != 1)
                throw new InvalidOperationException("Loaded module resolved to " + matches.Count + " files in " + directory);
            return matches[0];
        }

        private static AssemblyScanCache ReadCache(string path)
        {
            try
            {
                if (File.Exists(path)) return JsonConvert.DeserializeObject<AssemblyScanCache>(File.ReadAllText(path)) ?? new AssemblyScanCache();
            }
            catch (Exception exception) { Log.Warning("[KFCommonUtilityLib] Rebuilding compatibility cache: " + exception.Message); }
            return new AssemblyScanCache();
        }

        private static void SaveCache(string path, AssemblyScanCache cache)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(cache));
            }
            catch (Exception exception) { Log.Warning("[KFCommonUtilityLib] Compatibility cache was not saved: " + exception.Message); }
        }
    }
}
