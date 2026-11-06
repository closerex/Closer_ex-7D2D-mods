using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using KFAssemblyPatcher;
using Newtonsoft.Json;
using UnityEngine;

namespace KFCommonUtilityLib
{
    internal static class GameAssemblyCompatibilityGate
    {
        private const string DialogTitle = "KFCommonUtilityLib assembly update required";
        private const string ModName = "CommonUtilityLib";
        private const string GameAssemblyFileName = "Assembly-CSharp.dll";
        private const int RequiredDotNetMajorVersion = 10;
        private const string DotNetDownloadUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/10.0";

        /// <summary>
        /// 允许游戏启动器改写游戏程序集的唯一开关。
        /// 未设置时绝不修改任何文件，服务器会直接退出并提示该变量。
        /// 注意：它授权的是"修复"，不是"带病运行"——不兼容的服务器永远不会继续跑下去。
        /// </summary>
        private const string RepairConsentVariable = "KF_ALLOW_ASSEMBLY_REPAIR";

        private static int repairStarted;

        internal static void OnGameAwake(ref ModEvents.SGameAwakeData _)
        {
            var result = CheckCompatibility();
            if (result.State == CompatibilityState.Compatible)
            {
                return;
            }

            Log.Error("[KFCommonUtilityLib] " + result.Message);
            if (GameManager.IsDedicatedServer)
            {
                // 专用服务器没有 XUi（该程序集里不存在 XUiC_MessageBoxWindowGroup），
                // 但它的控制台本身就是 WinForms 窗体（WinFormConnection : System.Windows.Forms.Form），
                // 官方关闭窗口时也用 MessageBox 确认，所以这里用同样的方式给出选择，
                // 而不是静默退出 —— 运维看不到窗口时至少还能从日志得知原因。
                if (result.State == CompatibilityState.RuntimeMissing)
                {
                    Log.Error("[KFCommonUtilityLib] Install the .NET 10 Runtime from: " + DotNetDownloadUrl);
                }

                ShowServerDecisionDialog(result);
                return;
            }

            var xui = GameManager.Instance?.windowManager?.playerUI?.xui;
            if (xui == null)
            {
                Log.Error("[KFCommonUtilityLib] XUi is unavailable; using fail-closed exit flow.");
                if (result.State == CompatibilityState.RuntimeMissing)
                {
                    Log.Error("[KFCommonUtilityLib] Install the .NET 10 Runtime from: " + DotNetDownloadUrl);
                }

                StartRepairAndQuit(result);
                return;
            }

            XUiC_MessageBoxWindowGroup.ShowOk(
                xui,
                DialogTitle,
                BuildDialogText(result),
                string.Empty,
                () => ConfirmAndQuit(result),
                false,
                true,
                false);
        }

        private static CompatibilityResult CheckCompatibility()
        {
            try
            {
                var gameAssembly = typeof(ItemActionRanged).Assembly;
                if (typeof(MinEventActionModifyStats).Assembly != gameAssembly ||
                    typeof(ItemInventoryData).Assembly != gameAssembly)
                {
                    return CompatibilityResult.Fatal(
                        "The fixed target types were loaded from different assemblies. No repair was attempted.");
                }

                var dataPath = Application.dataPath;
                if (string.IsNullOrWhiteSpace(dataPath) || !Path.IsPathRooted(dataPath))
                {
                    return CompatibilityResult.Fatal(
                        "Unity did not provide an absolute game data directory. No installation path will be guessed.");
                }

                var assemblyPath = Path.GetFullPath(
                    Path.Combine(dataPath, "Managed", GameAssemblyFileName));
                if (!Path.IsPathRooted(assemblyPath) || !File.Exists(assemblyPath))
                {
                    return CompatibilityResult.Fatal(
                        "Unity's game data directory did not resolve to an existing absolute game assembly: " +
                        assemblyPath);
                }

                var methods = new[]
                {
                    ResolveMethod(
                        typeof(ItemActionRanged),
                        nameof(ItemActionRanged.TryExecuteAction),
                        typeof(void),
                        typeof(ItemActionData)),
                    ResolveMethod(
                        typeof(MinEventActionModifyStats),
                        "execute",
                        typeof(void),
                        typeof(MinEventParams))
                };
                var missing = methods.Where(method => method == null).ToArray();
                if (missing.Length != 0)
                {
                    return CompatibilityResult.Fatal(
                        "One or more fixed method signatures no longer exist. KFCommonUtilityLib must be updated " +
                        "for this game version before startup can continue.",
                        assemblyPath);
                }

                bool invalidFlags = typeof(ItemInventoryData).IsSealed ||
                    methods.Any(method => !method.IsVirtual || method.IsFinal ||
                        (method.Attributes & MethodAttributes.NewSlot) == 0);
                var scan = LoadedAssemblyCatalog.Scan(assemblyPath, gameAssembly, invalidFlags);
                if (scan.Report.Errors.Count != 0)
                    return CompatibilityResult.Fatal(string.Join("\n", scan.Report.Errors), assemblyPath);
                if (!scan.Report.NeedsRepair && !invalidFlags)
                {
                    return CompatibilityResult.Compatible(assemblyPath);
                }

                var runtime = DotNetRuntimeLocator.FindRequiredRuntime(RequiredDotNetMajorVersion);
                if (!runtime.IsAvailable)
                {
                    return CompatibilityResult.RuntimeMissing(
                        assemblyPath,
                        "Loaded assemblies require repair, but the .NET 10 Runtime is not installed or could " +
                        "not be found. " + runtime.Diagnostic);
                }

                return CompatibilityResult.NeedsRepair(
                    assemblyPath,
                    runtime.HostPath,
                    scan.Report.NeedsRepair ? string.Join("\n", scan.Report.Details) :
                        "The files are compatible, but this process still has sealed game types or non-overridable methods loaded. A restart is required.",
                    scan.Inputs);
            }
            catch (Exception exception)
            {
                return CompatibilityResult.Fatal("Compatibility check failed: " + exception);
            }
        }

        private static MethodInfo ResolveMethod(
            Type declaringType,
            string methodName,
            Type returnType,
            params Type[] parameterTypes)
        {
            var method = declaringType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null,
                parameterTypes,
                null);
            return method != null && method.ReturnType == returnType ? method : null;
        }

        private static string BuildDialogText(CompatibilityResult result)
        {
            if (result.State == CompatibilityState.Fatal)
            {
                return result.Message +
                       "\n\nThe game will close when you confirm. Review the game log before starting it again.";
            }

            if (result.State == CompatibilityState.RuntimeMissing)
            {
                return result.Message +
                       "\n\nInstall the Microsoft .NET 10 Runtime (Microsoft.NETCore.App 10.x):\n" +
                       DotNetDownloadUrl +
                       "\n\nConfirm to open the official Microsoft download page and close the game. " +
                       "Start the game again after installation.";
            }

            return result.Message +
                   "\n\nAssembly loaded by this game process:\n" + result.AssemblyPath +
                   "\n\nConfirm to close the game, repair the listed game/mod assembly files, and attempt to restart " +
                   "the same executable with the same launch arguments.";
        }

        /// <summary>
        /// 专用服务器的确认流程。要点：
        ///   1. 不静默退出——先把完整原因写进日志。
        ///   2. Windows 服务器的控制台本身就是 WinForms 窗体（WinFormConnection : Form），
        ///      官方关窗确认也用 MessageBox，所以这里弹 Yes/No。
        ///   3. Linux/macOS 服务器没有窗体；只有 stdin 连着终端时才读一行确认，
        ///      否则（systemd/容器/重定向）直接退出并给出确切的旁路指令，绝不挂起等待。
        ///   4. 无论哪个平台，选“否”一律直接退出：不允许带病运行。
        ///   5. 未设置 <see cref="RepairConsentVariable"/> 时绝不修改游戏程序集。
        /// </summary>
        private static void ShowServerDecisionDialog(CompatibilityResult result)
        {
            var needsRepair = result.State == CompatibilityState.NeedsRepair;

            if (needsRepair && !HasRepairConsent())
            {
                Log.Error("[KFCommonUtilityLib] Refusing to modify the game assembly: " +
                          RepairConsentVariable + " is not set to 1.");
                Log.Error("[KFCommonUtilityLib] To authorize the repair, start the server with " +
                          RepairConsentVariable + "=1 and confirm again.");
                QuitForIncompatibility(result, 3);
                return;
            }

            var question = BuildServerQuestion(result, needsRepair);
            ServerDecision decision;
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsServer:
                    decision = AskWithWindowsDialog(question);
                    break;
                default:
                    decision = AskOnConsole(question);
                    break;
            }

            if (decision == ServerDecision.Repair)
            {
                Log.Out("[KFCommonUtilityLib] Operator confirmed shutdown for assembly repair.");
                StartRepairAndQuit(result);
                return;
            }

            if (decision == ServerDecision.Declined)
            {
                Log.Error("[KFCommonUtilityLib] Operator declined; shutting down because the server " +
                          "cannot run with an incompatible assembly.");
            }
            else
            {
                Log.Error("[KFCommonUtilityLib] No interactive terminal is attached, so the mismatch " +
                          "cannot be confirmed. Shutting down.");
            }

            QuitForIncompatibility(result, decision == ServerDecision.Declined ? 4 : 5);
        }

        private static bool HasRepairConsent()
        {
            var value = Environment.GetEnvironmentVariable(RepairConsentVariable);
            return !string.IsNullOrWhiteSpace(value) && value.Trim() == "1";
        }

        private static string BuildServerQuestion(CompatibilityResult result, bool needsRepair)
        {
            var text = result.Message +
                       "\n\nAssembly loaded by this game process:\n" + result.AssemblyPath;
            return needsRepair
                ? text + "\n\nYes: close the server, repair the listed assembly files, then restart it." +
                  "\nNo: keep the files untouched and close the server."
                : text + "\n\nYes: close the server now.";
        }

        private static ServerDecision AskWithWindowsDialog(string question)
        {
            try
            {
                return ShowWindowsMessageBox(
                    DialogTitle,
                    question,
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Exclamation,
                    System.Windows.Forms.DialogResult.Yes)
                    ? ServerDecision.Repair
                    : ServerDecision.Declined;
            }
            catch (Exception exception)
            {
                Log.Error("[KFCommonUtilityLib] Could not show the confirmation dialog: " + exception);
                return ServerDecision.Unavailable;
            }
        }

        /// <summary>
        /// 只有标准输入确实连在终端上时才提问；被重定向或被调度器拉起时立即返回 Unavailable，
        /// 避免读一行就阻塞在无人回应的服务器上。
        /// </summary>
        private static ServerDecision AskOnConsole(string question)
        {
            try
            {
                if (Console.IsInputRedirected || Console.IsOutputRedirected)
                {
                    return ServerDecision.Unavailable;
                }

                Console.Out.WriteLine();
                Console.Out.WriteLine("=== " + DialogTitle + " ===");
                Console.Out.WriteLine(question);
                Console.Out.Write("Type YES to repair and close, or anything else to close without changes: ");
                Console.Out.Flush();

                var answer = Console.In.ReadLine();
                if (answer != null && answer.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase))
                {
                    return ServerDecision.Repair;
                }

                return ServerDecision.Declined;
            }
            catch (Exception exception)
            {
                Log.Error("[KFCommonUtilityLib] Could not read the console confirmation: " + exception);
                return ServerDecision.Unavailable;
            }
        }

        /// <summary>
        /// 用全限定名调用 WinForms：本文件已经 using UnityEngine，而 UnityEngine 也有
        /// MessageBox / DialogResult 同名类型，不限定会歧义。
        /// </summary>
        private static bool ShowWindowsMessageBox(
            string title,
            string text,
            System.Windows.Forms.MessageBoxButtons buttons,
            System.Windows.Forms.MessageBoxIcon icon,
            System.Windows.Forms.DialogResult expected)
        {
            return System.Windows.Forms.MessageBox.Show(
                       text,
                       title,
                       buttons,
                       icon) == expected;
        }

        private enum ServerDecision
        {
            Repair,
            Declined,
            Unavailable
        }

        /// <summary>
        /// 不兼容的服务器一律退出。用 Environment.Exit 而不是 Application.Quit：
        /// 这是纯命令行进程，需要确定的退出与退出码（供 systemd/容器判断），
        /// 而且外部补丁进程正在等待本进程结束。
        /// </summary>
        private static void QuitForIncompatibility(CompatibilityResult result, int exitCode)
        {
            try
            {
                Log.Error("[KFCommonUtilityLib] Dedicated server shutting down (exit code " + exitCode + ").");
            }
            catch
            {
                // 日志失败也必须退出。
            }

            Environment.Exit(exitCode);
        }

        private static void ConfirmAndQuit(CompatibilityResult result)
        {
            if (result.State == CompatibilityState.RuntimeMissing)
            {
                Application.OpenURL(DotNetDownloadUrl);
            }

            StartRepairAndQuit(result);
        }

        private static void StartRepairAndQuit(CompatibilityResult result)
        {
            if (Interlocked.CompareExchange(ref repairStarted, 1, 0) != 0)
            {
                return;
            }

            if (result.State == CompatibilityState.NeedsRepair)
            {
                try
                {
                    StartRepairProcess(result.AssemblyPath, result.DotNetHostPath, result.Inputs);
                }
                catch (Exception exception)
                {
                    Log.Error("[KFCommonUtilityLib] Could not start assembly repair: " + exception);
                }
            }

            QuitApplication();
        }

        /// <summary>
        /// 与官方服务器控制台的关窗确认保持一致：把退出排到主线程执行，
        /// 避免在非主线程直接调用 Application.Quit()。
        /// 补丁进程本身会等待本进程结束，所以这里的退出时机就是修复开始生效的时机。
        /// </summary>
        private static void QuitApplication()
        {
            try
            {
                ThreadManager.AddSingleTaskMainThread("KFCommonUtilityLib.Shutdown",
                    () => Application.Quit());
            }
            catch (Exception exception)
            {
                Log.Error("[KFCommonUtilityLib] Could not queue shutdown on the main thread: " + exception);
                Application.Quit();
            }
        }

        private static void StartRepairProcess(string assemblyPath, string dotNetHostPath, AssemblyScanInput[] inputs)
        {
            if (string.IsNullOrWhiteSpace(dotNetHostPath) ||
                !Path.IsPathRooted(dotNetHostPath) ||
                !File.Exists(dotNetHostPath))
            {
                throw new InvalidOperationException(
                    "The verified dotnet host path is no longer an existing absolute file: " + dotNetHostPath);
            }

            var mod = ModManager.GetMod(ModName, true);
            if (mod == null)
            {
                throw new InvalidOperationException(
                    "ModManager could not find the loaded " + ModName + " mod.");
            }

            if (string.IsNullOrWhiteSpace(mod.Path))
            {
                throw new InvalidOperationException(
                    "ModManager returned an empty path for the loaded " + ModName + " mod.");
            }

            if (!Path.IsPathRooted(mod.Path))
            {
                throw new InvalidOperationException(
                    "ModManager returned a non-absolute path for the loaded " + ModName + " mod: " + mod.Path);
            }

            var modDirectory = Path.GetFullPath(mod.Path);
            if (!Directory.Exists(modDirectory))
            {
                throw new InvalidOperationException(
                    "ModManager returned a non-existing mod directory: " + modDirectory);
            }

            var toolsDirectory = Path.Combine(modDirectory, "Tools", "AssemblyPatcher");
            var logDirectory = Path.Combine(Path.GetTempPath(), "KFCommonUtilityLib");
            Directory.CreateDirectory(logDirectory);

            using (var currentProcess = Process.GetCurrentProcess())
            {
                var logPath = Path.Combine(
                    logDirectory,
                    "AssemblyPatcher-" + currentProcess.Id + ".log");
                var executablePath = currentProcess.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathRooted(executablePath))
                {
                    throw new InvalidOperationException("Current game executable path is unavailable or not absolute.");
                }

                var request = new RestartRequest
                {
                    GameExecutablePath = Path.GetFullPath(executablePath),
                    WorkingDirectory = Path.GetFullPath(Environment.CurrentDirectory),
                    Arguments = Environment.GetCommandLineArgs().Skip(1).ToArray(),
                    OriginProcessStartTimeUtcTicks = currentProcess.StartTime.ToUniversalTime().Ticks,
                    RestartAfterPatch = true,
                    AssemblyManifestPath = Path.Combine(logDirectory,
                        "AssemblyManifest-" + currentProcess.Id + "-" + currentProcess.StartTime.ToUniversalTime().Ticks + ".json")
                };
                // Keep large mod collections out of the command-line length limit.
                File.WriteAllText(request.AssemblyManifestPath, JsonConvert.SerializeObject(inputs));
                var encodedRequest = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request)));
                var startInfo = CreateWrapperStartInfo(
                    toolsDirectory,
                    Path.GetFullPath(dotNetHostPath),
                    Path.GetFullPath(assemblyPath),
                    currentProcess.Id,
                    encodedRequest,
                    logPath);

                Log.Out("[KFCommonUtilityLib] Starting assembly repair for: " + assemblyPath);
                Log.Out("[KFCommonUtilityLib] Assembly repair log: " + logPath);
                using (var wrapperProcess = Process.Start(startInfo))
                {
                    if (wrapperProcess == null)
                    {
                        throw new InvalidOperationException("The operating system did not start the repair wrapper.");
                    }
                }
            }
        }

        private static ProcessStartInfo CreateWrapperStartInfo(
            string toolsDirectory,
            string dotNetHostPath,
            string assemblyPath,
            int processId,
            string encodedRequest,
            string logPath)
        {
            string executable;
            bool isWindows;
            var arguments = new List<string>();
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsServer:
                    isWindows = true;
                    executable = "powershell.exe";
                    arguments.Add("-NoProfile");
                    arguments.Add("-ExecutionPolicy");
                    arguments.Add("Bypass");
                    arguments.Add("-File");
                    arguments.Add(Path.Combine(toolsDirectory, "launch-patcher.ps1"));
                    arguments.Add("-DotNetPath");
                    arguments.Add(dotNetHostPath);
                    arguments.Add("-AssemblyPath");
                    arguments.Add(assemblyPath);
                    arguments.Add("-ProcessId");
                    arguments.Add(processId.ToString());
                    arguments.Add("-Request");
                    arguments.Add(encodedRequest);
                    arguments.Add("-LogPath");
                    arguments.Add(logPath);
                    break;

                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxServer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXServer:
                    isWindows = false;
                    executable = "/bin/sh";
                    VerifyShellWrapperPresent(Path.Combine(toolsDirectory, "launch-patcher.sh"));
                    arguments.Add(Path.Combine(toolsDirectory, "launch-patcher.sh"));
                    arguments.Add("--dotnet");
                    arguments.Add(dotNetHostPath);
                    arguments.Add("--assembly");
                    arguments.Add(assemblyPath);
                    arguments.Add("--pid");
                    arguments.Add(processId.ToString());
                    arguments.Add("--request");
                    arguments.Add(encodedRequest);
                    arguments.Add("--log");
                    arguments.Add(logPath);
                    break;

                default:
                    throw new PlatformNotSupportedException(
                        "Assembly repair is not packaged for " + Application.platform + ".");
            }

            return new ProcessStartInfo
            {
                FileName = executable,
                Arguments = JoinProcessArguments(arguments, isWindows),
                WorkingDirectory = toolsDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        /// <summary>
        /// 启动前确认 shell 包装脚本存在。目标框架是 net48，没有 File.GetUnixFileMode
        /// （.NET 6+ API），所以不探测权限位。
        /// 也不需要可执行位：本工程始终以 "/bin/sh 脚本" 的方式调用。
        /// </summary>
        private static void VerifyShellWrapperPresent(string wrapperPath)
        {
            if (File.Exists(wrapperPath))
            {
                Log.Out("[KFCommonUtilityLib] Repair wrapper: " + wrapperPath +
                        " (invoked through /bin/sh; the execute bit is not required)");
                return;
            }

            Log.Error("[KFCommonUtilityLib] Shell repair wrapper is missing: " + wrapperPath);
        }

        /// <summary>
        /// 参数转义必须按目标平台的规则来：
        /// Windows 走 PowerShell 的参数解析规则，Linux/macOS 走 /bin/sh 的 POSIX 规则。
        /// 两者不通用 —— 用 Windows 那套去拼 /bin/sh 的命令行会把路径拼坏。
        /// </summary>
        private static string JoinProcessArguments(List<string> arguments, bool isWindows)
        {
            return string.Join(" ", arguments.Select(
                isWindows ? (Func<string, string>)QuoteWindowsArgument : QuotePosixArgument));
        }

        private static string QuoteWindowsArgument(string value)
        {
            if (value.Length != 0 && value.All(character => !char.IsWhiteSpace(character) && character != '"'))
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            var backslashCount = 0;
            foreach (var character in value)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append('"');
                    backslashCount = 0;
                    continue;
                }

                builder.Append('\\', backslashCount);
                backslashCount = 0;
                builder.Append(character);
            }

            builder.Append('\\', backslashCount * 2);
            builder.Append('"');
            return builder.ToString();
        }

        /// <summary>
        /// POSIX 单引号转义：单引号内除单引号本身外一切都是字面量，
        /// 单引号用 '\'' 表示。这是 /bin/sh 下唯一无歧义的做法。
        /// </summary>
        private static string QuotePosixArgument(string value)
        {
            if (value.Length != 0 && value.All(character => !char.IsWhiteSpace(character) &&
                                                            character != '\'' &&
                                                            character != '"' &&
                                                            character != '\\' &&
                                                            character != '$' &&
                                                            character != '`'))
            {
                return value;
            }

            return "'" + value.Replace("'", "'\\''") + "'";
        }

        private enum CompatibilityState
        {
            Compatible,
            NeedsRepair,
            RuntimeMissing,
            Fatal
        }

        private sealed class CompatibilityResult
        {
            private CompatibilityResult(
                CompatibilityState state,
                string assemblyPath,
                string dotNetHostPath,
                string message)
            {
                State = state;
                AssemblyPath = assemblyPath;
                DotNetHostPath = dotNetHostPath;
                Message = message;
            }

            internal CompatibilityState State { get; }
            internal string AssemblyPath { get; }
            internal string DotNetHostPath { get; }
            internal string Message { get; }
            internal AssemblyScanInput[] Inputs { get; private set; }

            internal static CompatibilityResult Compatible(string assemblyPath)
            {
                return new CompatibilityResult(
                    CompatibilityState.Compatible,
                    assemblyPath,
                    string.Empty,
                    string.Empty);
            }

            internal static CompatibilityResult NeedsRepair(
                string assemblyPath,
                string dotNetHostPath,
                string message,
                AssemblyScanInput[] inputs)
            {
                return new CompatibilityResult(
                    CompatibilityState.NeedsRepair,
                    assemblyPath,
                    dotNetHostPath,
                    message) { Inputs = inputs };
            }

            internal static CompatibilityResult RuntimeMissing(string assemblyPath, string message)
            {
                return new CompatibilityResult(
                    CompatibilityState.RuntimeMissing,
                    assemblyPath,
                    string.Empty,
                    message);
            }

            internal static CompatibilityResult Fatal(string message, string assemblyPath = "")
            {
                return new CompatibilityResult(
                    CompatibilityState.Fatal,
                    assemblyPath,
                    string.Empty,
                    message);
            }
        }

        private sealed class RestartRequest
        {
            public string GameExecutablePath { get; set; }
            public string WorkingDirectory { get; set; }
            public string[] Arguments { get; set; }
            public long OriginProcessStartTimeUtcTicks { get; set; }
            public bool RestartAfterPatch { get; set; }
            public string AssemblyManifestPath { get; set; }
        }
    }
}
