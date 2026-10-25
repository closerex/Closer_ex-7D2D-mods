using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace KFAssemblyPatcher
{
    public sealed class CommandLineOptions
    {
        private CommandLineOptions(
            string assemblyPath,
            int originProcessId,
            string encodedRequest,
            string logPath)
        {
            AssemblyPath = assemblyPath;
            OriginProcessId = originProcessId;
            EncodedRequest = encodedRequest;
            LogPath = logPath;
        }

        public string AssemblyPath { get; }
        public int OriginProcessId { get; }
        public string EncodedRequest { get; }
        public string LogPath { get; }

        public static CommandLineOptions Parse(string[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (args.Length == 0 || args.Length % 2 != 0)
            {
                throw new AssemblyPatchException(
                    "Expected --assembly, --pid, --request, and --log value pairs.");
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index += 2)
            {
                var name = args[index];
                if (name != "--assembly" && name != "--pid" && name != "--request" && name != "--log")
                {
                    throw new AssemblyPatchException("Unknown argument: " + name);
                }

                if (!values.TryAdd(name, args[index + 1]))
                {
                    throw new AssemblyPatchException("Duplicate argument: " + name);
                }
            }

            var assemblyPath = GetRequired(values, "--assembly");
            if (!Path.IsPathRooted(assemblyPath))
            {
                throw new AssemblyPatchException("Assembly path must be absolute.");
            }

            var logPath = GetRequired(values, "--log");
            if (!Path.IsPathRooted(logPath))
            {
                throw new AssemblyPatchException("Log path must be absolute.");
            }

            if (!int.TryParse(
                    GetRequired(values, "--pid"),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var processId) || processId <= 0)
            {
                throw new AssemblyPatchException("Origin process ID is invalid.");
            }

            return new CommandLineOptions(
                Path.GetFullPath(assemblyPath),
                processId,
                GetRequired(values, "--request"),
                Path.GetFullPath(logPath));
        }

        private static string GetRequired(IReadOnlyDictionary<string, string> values, string name)
        {
            if (!values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new AssemblyPatchException("Missing argument: " + name);
            }

            return value;
        }
    }
}
