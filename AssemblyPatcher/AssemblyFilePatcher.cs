using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;

namespace KFAssemblyPatcher
{
    public static class AssemblyFilePatcher
    {
        public sealed class FileSetChange
        {
            public string Path { get; internal set; } = string.Empty;
            public VirtualizationResult Transformation { get; internal set; } = null!;
        }

        private sealed class StagedChange
        {
            internal AssemblyScanInput Input = null!;
            internal string TemporaryPath = string.Empty;
            internal string OriginalHash = string.Empty;
            internal string RollbackPath = string.Empty;
            internal VirtualizationResult Result = null!;
        }

        // No backup is left next to the game assemblies: the directory stays clean, and
        // Steam's "verify integrity of game files" restores any original. Rollback copies
        // live in a per-run temporary directory only for as long as the commit takes,
        // because the automatic restore below must be able to undo a half-applied repair
        // without asking the user to leave the game and re-download anything.
        public static IReadOnlyList<FileSetChange> PatchSet(string assemblyPath, IReadOnlyList<AssemblyScanInput> manifest)
        {
            string gamePath = ValidateAssemblyPath(assemblyPath);
            if (manifest == null || manifest.Count == 0)
                manifest = new[] { AssemblyScanInput.FromFile(gamePath) };
            if (!AssemblyCollectionScanner.PathComparer.Equals(gamePath, manifest[0].Path))
                throw new AssemblyPatchException("The repair manifest does not start with the selected game assembly.");
            var inputs = manifest.GroupBy(i => i.Path, AssemblyCollectionScanner.PathComparer).Select(g => g.First()).ToArray();
            foreach (var input in inputs)
            {
                if (!Path.IsPathRooted(input.Path)) throw new AssemblyPatchException("Manifest path is not absolute: " + input.Path);
                if (!input.Matches(AssemblyScanInput.Create(input.Path, input.AssemblyName, input.Mvid)))
                    throw new AssemblyPatchException("Assembly changed after the startup scan: " + input.Path);
            }
            var staged = new List<StagedChange>();
            var committed = new List<StagedChange>();
            string? rollbackDirectory = null;
            try
            {
                using (var resolver = AssemblyCollectionScanner.CreateResolver(inputs))
                using (var game = AssemblyCollectionScanner.Read(gamePath, resolver))
                {
                    inputs[0].VerifyIdentity(game);
                    // Every candidate is read up front. An override that decides whether a
                    // call site may be rewritten lives in a module assembly, so the repair
                    // needs the whole collection in hand before it transforms anything --
                    // including when it transforms the game assembly itself.
                    var modules = new List<AssemblyDefinition>(Math.Max(0, inputs.Length - 1));
                    try
                    {
                        for (int index = 1; index < inputs.Length; index++)
                        {
                            var mod = AssemblyCollectionScanner.Read(inputs[index].Path, resolver);
                            inputs[index].VerifyIdentity(mod);
                            modules.Add(mod);
                        }

                        Stage(inputs[0], game, AssemblyVirtualizer.Transform(game, modules));
                        for (int index = 0; index < modules.Count; index++)
                            Stage(inputs[index + 1], modules[index],
                                AssemblyVirtualizer.TransformMod(modules[index], game, modules));

                        // Every candidate has been transformed before any original is replaced.
                        foreach (var change in staged)
                        {
                            using var verification = AssemblyCollectionScanner.Read(change.TemporaryPath, resolver);
                            if (AssemblyCollectionScanner.PathComparer.Equals(change.Input.Path, gamePath))
                                AssemblyVirtualizer.Verify(verification, modules);
                            else AssemblyVirtualizer.VerifyMod(verification, game, modules);
                        }
                    }
                    finally
                    {
                        foreach (var mod in modules) mod.Dispose();
                    }
                }
                foreach (var change in staged)
                {
                    if (!change.Input.Matches(AssemblyScanInput.Create(change.Input.Path, change.Input.AssemblyName, change.Input.Mvid)) ||
                        ComputeSha256(change.Input.Path) != change.OriginalHash)
                        throw new AssemblyPatchException("Assembly changed while preparing repair: " + change.Input.Path);
                    if (rollbackDirectory == null) rollbackDirectory = CreateRollbackDirectory();
                    change.RollbackPath = Path.Combine(rollbackDirectory, Path.GetFileName(change.Input.Path));
                    File.Copy(change.Input.Path, change.RollbackPath, overwrite: false);
                    if (ComputeSha256(change.RollbackPath) != change.OriginalHash)
                        throw new AssemblyPatchException("Rollback copy verification failed: " + change.RollbackPath);
                }
                foreach (var change in staged)
                {
                    File.Move(change.TemporaryPath, change.Input.Path, overwrite: true);
                    committed.Add(change);
                }
                return staged.Select(change => new FileSetChange { Path = change.Input.Path,
                    Transformation = change.Result }).ToArray();
            }
            catch (Exception failure)
            {
                var rollbackFailures = new List<string>();
                for (int index = committed.Count - 1; index >= 0; index--)
                {
                    var change = committed[index];
                    try
                    {
                        File.Copy(change.RollbackPath, change.Input.Path, overwrite: true);
                        if (ComputeSha256(change.Input.Path) != change.OriginalHash)
                            rollbackFailures.Add(change.Input.Path + ": restored bytes do not match the original");
                    }
                    catch (Exception rollback) { rollbackFailures.Add(change.Input.Path + ": " + rollback.Message); }
                }
                if (rollbackFailures.Count != 0)
                    throw new AssemblyPatchException(
                        "Repair failed and automatic rollback is incomplete; restore the originals with Steam " +
                        "(verify integrity of game files): " + string.Join("; ", rollbackFailures), failure);
                if (failure is AssemblyPatchException) throw;
                throw new AssemblyPatchException("Assembly collection repair failed; completed replacements were restored.", failure);
            }
            finally
            {
                foreach (var change in staged)
                    if (File.Exists(change.TemporaryPath)) File.Delete(change.TemporaryPath);
                DeleteRollbackDirectory(rollbackDirectory);
            }

            void Stage(AssemblyScanInput input, AssemblyDefinition assembly, VirtualizationResult result)
            {
                if (!result.Changed) return;
                var change = new StagedChange { Input = input, Result = result,
                    OriginalHash = ComputeSha256(input.Path),
                    TemporaryPath = input.Path + ".kfpatch." + Guid.NewGuid().ToString("N") + ".tmp" };
                staged.Add(change);
                assembly.Write(change.TemporaryPath);
            }
        }

        private static string CreateRollbackDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "KFAssemblyPatcher", "rollback-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteRollbackDirectory(string? directory)
        {
            if (string.IsNullOrEmpty(directory)) return;
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static DefaultAssemblyResolver CreateResolver(string assemblyPath)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath)!);
            return resolver;
        }

        private static string ValidateAssemblyPath(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                throw new AssemblyPatchException("Assembly path is empty.");
            }

            var fullPath = Path.GetFullPath(assemblyPath);
            if (!Path.IsPathRooted(fullPath) || !File.Exists(fullPath))
            {
                throw new AssemblyPatchException("Assembly path is not an existing absolute file: " + fullPath);
            }

            return fullPath;
        }

        private static string ComputeSha256(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }
    }
}
