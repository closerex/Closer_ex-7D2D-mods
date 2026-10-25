using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KFCommonUtilityLib
{
    internal sealed class DotNetRuntimeCheckResult
    {
        private DotNetRuntimeCheckResult(bool isAvailable, string hostPath, string diagnostic)
        {
            IsAvailable = isAvailable;
            HostPath = hostPath;
            Diagnostic = diagnostic;
        }

        internal bool IsAvailable { get; }
        internal string HostPath { get; }
        internal string Diagnostic { get; }

        internal static DotNetRuntimeCheckResult Available(string hostPath)
        {
            return new DotNetRuntimeCheckResult(true, hostPath, string.Empty);
        }

        internal static DotNetRuntimeCheckResult Missing(string diagnostic)
        {
            return new DotNetRuntimeCheckResult(false, string.Empty, diagnostic);
        }
    }

    internal static class DotNetRuntimeLocator
    {
        private const int RuntimeQueryTimeoutMilliseconds = 5000;

        internal static DotNetRuntimeCheckResult FindRequiredRuntime(int requiredMajorVersion)
        {
            return FindRequiredRuntime(
                requiredMajorVersion,
                GetCandidateHostPaths(),
                File.Exists,
                QueryInstalledRuntimes);
        }

        internal static DotNetRuntimeCheckResult FindRequiredRuntime(
            int requiredMajorVersion,
            IEnumerable<string> candidatePaths,
            Func<string, bool> fileExists,
            Func<string, string> runtimeListProvider)
        {
            if (requiredMajorVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredMajorVersion));
            }

            if (candidatePaths == null)
            {
                throw new ArgumentNullException(nameof(candidatePaths));
            }

            if (fileExists == null)
            {
                throw new ArgumentNullException(nameof(fileExists));
            }

            if (runtimeListProvider == null)
            {
                throw new ArgumentNullException(nameof(runtimeListProvider));
            }

            var checkedHosts = new List<string>();
            var failures = new List<string>();
            foreach (var candidate in candidatePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (!Path.IsPathRooted(candidate))
                {
                    continue;
                }

                var hostPath = Path.GetFullPath(candidate);
                if (checkedHosts.Contains(hostPath, GetPathComparer()))
                {
                    continue;
                }

                checkedHosts.Add(hostPath);
                if (!fileExists(hostPath))
                {
                    continue;
                }

                try
                {
                    var runtimeList = runtimeListProvider(hostPath);
                    if (ContainsRequiredRuntime(runtimeList, requiredMajorVersion))
                    {
                        return DotNetRuntimeCheckResult.Available(hostPath);
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(hostPath + ": " + exception.Message);
                }
            }

            var diagnostic =
                "Microsoft.NETCore.App " + requiredMajorVersion.ToString(CultureInfo.InvariantCulture) +
                ".x was not found. Checked dotnet hosts: " +
                (checkedHosts.Count == 0 ? "none" : string.Join(", ", checkedHosts)) + ".";
            if (failures.Count != 0)
            {
                diagnostic += " Host query failures: " + string.Join(" | ", failures) + ".";
            }

            return DotNetRuntimeCheckResult.Missing(diagnostic);
        }

        internal static bool ContainsRequiredRuntime(string runtimeList, int requiredMajorVersion)
        {
            if (string.IsNullOrWhiteSpace(runtimeList) || requiredMajorVersion <= 0)
            {
                return false;
            }

            var prefix =
                "Microsoft.NETCore.App " + requiredMajorVersion.ToString(CultureInfo.InvariantCulture) + ".";
            return runtimeList
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
        }

        private static IEnumerable<string> GetCandidateHostPaths()
        {
            var hostFileName = Path.DirectorySeparatorChar == '\\' ? "dotnet.exe" : "dotnet";
            foreach (var variableName in new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_ARM64" })
            {
                var root = Environment.GetEnvironmentVariable(variableName);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    yield return Path.Combine(root.Trim().Trim('"'), hostFileName);
                }
            }

            var pathValue = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathValue))
            {
                foreach (var directory in pathValue.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        yield return Path.Combine(directory.Trim().Trim('"'), hostFileName);
                    }
                }
            }

            if (Path.DirectorySeparatorChar == '\\')
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrWhiteSpace(programFiles))
                {
                    yield return Path.Combine(programFiles, "dotnet", hostFileName);
                }

                if (!string.IsNullOrWhiteSpace(programFilesX86))
                {
                    yield return Path.Combine(programFilesX86, "dotnet", hostFileName);
                }
            }
            else
            {
                yield return "/usr/bin/dotnet";
                yield return "/usr/local/bin/dotnet";
                yield return "/usr/share/dotnet/dotnet";
                yield return "/usr/local/share/dotnet/dotnet";
                yield return "/opt/homebrew/bin/dotnet";
            }
        }

        private static string QueryInstalledRuntimes(string hostPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = hostPath,
                Arguments = "--list-runtimes",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("The operating system did not start the dotnet host.");
                }

                if (!process.WaitForExit(RuntimeQueryTimeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    throw new TimeoutException("dotnet --list-runtimes exceeded five seconds.");
                }

                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "dotnet --list-runtimes exited with code " + process.ExitCode + ": " + standardError);
                }

                return standardOutput;
            }
        }

        private static StringComparer GetPathComparer()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }
    }
}
