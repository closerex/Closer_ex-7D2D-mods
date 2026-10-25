using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace KFAssemblyPatcher
{
    public sealed class PatchRequest
    {
        public string GameExecutablePath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string[] Arguments { get; set; } = Array.Empty<string>();
        public long OriginProcessStartTimeUtcTicks { get; set; }
        public bool RestartAfterPatch { get; set; }
        public string? AssemblyManifestPath { get; set; }

        public AssemblyScanInput[] ReadManifest()
        {
            if (string.IsNullOrEmpty(AssemblyManifestPath)) return Array.Empty<AssemblyScanInput>();
            if (!Path.IsPathRooted(AssemblyManifestPath))
                throw new AssemblyPatchException("Assembly manifest path must be absolute.");
            var result = JsonSerializer.Deserialize<AssemblyScanInput[]>(File.ReadAllText(AssemblyManifestPath));
            if (result == null || result.Length == 0 || Array.Exists(result, entry => entry == null))
                throw new AssemblyPatchException("Assembly manifest is empty or invalid.");
            return result;
        }

        public static PatchRequest Decode(string encodedRequest)
        {
            if (string.IsNullOrWhiteSpace(encodedRequest))
            {
                throw new AssemblyPatchException("Encoded restart request is empty.");
            }

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedRequest));
                var request = JsonSerializer.Deserialize<PatchRequest>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request == null)
                {
                    throw new AssemblyPatchException("Restart request is empty.");
                }

                request.Validate();
                return request;
            }
            catch (AssemblyPatchException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new AssemblyPatchException("Restart request is invalid.", exception);
            }
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(GameExecutablePath) || !Path.IsPathRooted(GameExecutablePath))
            {
                throw new AssemblyPatchException("Game executable path must be absolute.");
            }

            GameExecutablePath = Path.GetFullPath(GameExecutablePath);
            if (!File.Exists(GameExecutablePath))
            {
                throw new AssemblyPatchException("Game executable does not exist: " + GameExecutablePath);
            }

            if (string.IsNullOrWhiteSpace(WorkingDirectory) || !Path.IsPathRooted(WorkingDirectory))
            {
                throw new AssemblyPatchException("Game working directory must be absolute.");
            }

            WorkingDirectory = Path.GetFullPath(WorkingDirectory);
            if (!Directory.Exists(WorkingDirectory))
            {
                throw new AssemblyPatchException("Game working directory does not exist: " + WorkingDirectory);
            }

            Arguments ??= Array.Empty<string>();
            if (OriginProcessStartTimeUtcTicks <= 0)
            {
                throw new AssemblyPatchException("Origin process start time is invalid.");
            }
        }
    }

    public static class RestartLauncher
    {
        public static ProcessStartInfo CreateStartInfo(PatchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = request.GameExecutablePath,
                WorkingDirectory = request.WorkingDirectory,
                UseShellExecute = false
            };
            foreach (var argument in request.Arguments)
            {
                startInfo.ArgumentList.Add(argument ?? string.Empty);
            }

            return startInfo;
        }
    }
}
