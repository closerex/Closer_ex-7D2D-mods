using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace KFAssemblyPatcher
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            PatchLogger? logger = null;
            try
            {
                var options = CommandLineOptions.Parse(args);
                logger = new PatchLogger(options.LogPath);
                var request = PatchRequest.Decode(options.EncodedRequest);

                logger.Write("Waiting for game process " + options.OriginProcessId + ".");
                WaitForOriginProcess(
                    options.OriginProcessId,
                    request.OriginProcessStartTimeUtcTicks,
                    logger);

                logger.Write("Patching absolute assembly path: " + options.AssemblyPath);
                var result = AssemblyFilePatcher.PatchSet(options.AssemblyPath, request.ReadManifest());
                if (result.Count != 0)
                {
                    foreach (var change in result)
                    {
                        logger.Write(
                            "Patch verified: " + change.Path + ". Types unsealed: " + change.Transformation.TypesChanged +
                            ", methods changed: " + change.Transformation.MethodsChanged +
                            ", call sites changed: " + change.Transformation.CallSitesChanged + ".");
                    }
                }
                else
                {
                    logger.Write("All assemblies were already compatible; no file was rewritten.");
                }

                if (request.RestartAfterPatch)
                {
                    var startInfo = RestartLauncher.CreateStartInfo(request);
                    logger.Write("Restarting: " + startInfo.FileName);
                    var restarted = Process.Start(startInfo);
                    if (restarted == null)
                    {
                        throw new AssemblyPatchException("The operating system did not start the game process.");
                    }

                    logger.Write("Restarted game process " + restarted.Id + ".");
                }

                return 0;
            }
            catch (Exception exception)
            {
                var message = "Assembly patch workflow failed: " + exception;
                if (logger != null)
                {
                    logger.Write(message);
                }
                else
                {
                    Console.Error.WriteLine(message);
                }

                return 1;
            }
            finally
            {
                logger?.Dispose();
            }
        }

        private static void WaitForOriginProcess(int processId, long expectedStartTimeUtcTicks, PatchLogger logger)
        {
            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                logger.Write("Origin process has already exited.");
                return;
            }

            using (process)
            {
                var actualStartTicks = process.StartTime.ToUniversalTime().Ticks;
                var startTimeDifference = Math.Abs(actualStartTicks - expectedStartTimeUtcTicks);
                if (startTimeDifference > TimeSpan.FromSeconds(2).Ticks)
                {
                    logger.Write(
                        "PID was reused; expected start ticks " +
                        expectedStartTimeUtcTicks.ToString(CultureInfo.InvariantCulture) +
                        " but found " + actualStartTicks.ToString(CultureInfo.InvariantCulture) +
                        ". The unrelated process will not be waited on.");
                    return;
                }

                process.WaitForExit();
                logger.Write("Origin process exited.");
            }
        }

        private sealed class PatchLogger : IDisposable
        {
            private readonly StreamWriter writer;

            internal PatchLogger(string path)
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                writer = new StreamWriter(path, append: true) { AutoFlush = true };
            }

            internal void Write(string message)
            {
                var line = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture) + " " + message;
                writer.WriteLine(line);
                Console.WriteLine(line);
            }

            public void Dispose()
            {
                writer.Dispose();
            }
        }
    }
}
