using System;
using System.Collections.Generic;
using System.Globalization;
using LuaRuntimeProbe.Backends;
using LuaRuntimeProbe.Contracts;
using LuaRuntimeProbe.Harness;
using UnityEngine.Scripting;

namespace LuaRuntimeProbe.Console
{
    /// <summary>
    /// The `luaprobe` console command.
    ///
    /// Registration is automatic: SdtdConsole.RegisterCommands() reflects over
    /// every loaded assembly for types implementing IConsoleCommand, so simply
    /// defining this class is enough - no Harmony patch is required.
    /// </summary>
    [Preserve]
    public class ConsoleCmdLuaProbe : ConsoleCmdAbstract
    {
        public override string[] getCommands()
        {
            return new[] { "luaprobe", "lrp" };
        }

        public override string getDescription()
        {
            return "Evaluate embeddable Lua runtimes (MoonSharp / NLua / xLua) inside 7 Days to Die.";
        }

        public override string getHelp()
        {
            return "luaprobe help | status | list | ctx | test [backend|all] [--caps] | "
                   + "run <backend> <lua-path> [args...] | bench [backend|all] [iterations] [budgetMs] | reset";
        }

        // Usable from the main menu so that status/test can prove the explicit
        // "no world loaded" behaviour of the read-only context.
        public override bool AllowedInMainMenu
        {
            get { return true; }
        }

        public override int DefaultPermissionLevel
        {
            get { return 0; }
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            // The command can run before IModApi.InitMod, so install the host here too (idempotent).
            Game.GameProbeHost.Install();

            // Backends (and their bridges) survive across commands and across world
            // changes, so any cached player snapshot must be dropped before this
            // command reads it - otherwise a later command could report stale data
            // from a previous world.
            LuaBackendRegistry.InvalidatePlayerSnapshots();

            var output = new ProbeOutput();

            try
            {
                string subcommand = _params != null && _params.Count > 0
                    ? _params[0].ToLowerInvariant()
                    : "help";

                var rest = new List<string>();
                if (_params != null)
                {
                    for (int i = 1; i < _params.Count; i++)
                    {
                        rest.Add(_params[i]);
                    }
                }

                switch (subcommand)
                {
                    case "help":
                    case "?":
                        RenderHelp(output);
                        break;
                    case "status":
                        RenderStatus(output);
                        break;
                    case "list":
                        RenderList(output);
                        break;
                    case "ctx":
                        RenderContext(output);
                        break;
                    case "test":
                        if (!RefuseWhileBenchDriving(output, "test"))
                        {
                            break;
                        }

                        RunTest(output, rest);
                        break;
                    case "run":
                        if (!RefuseWhileBenchDriving(output, "run"))
                        {
                            break;
                        }

                        RunScript(output, rest);
                        break;
                    case "bench":
                        RunBench(output, rest);
                        break;
                    case "reset":
                        // reset tears down every backend's Lua state, which would corrupt
                        // a cross-frame benchmark mid-measurement.
                        if (!RefuseWhileBenchDriving(output, "reset"))
                        {
                            break;
                        }

                        LuaBackendRegistry.ShutdownAll();
                        output.Line("all backends shut down; next command will re-initialise them");
                        break;
                    default:
                        output.Line("unknown subcommand: " + subcommand);
                        RenderHelp(output);
                        break;
                }
            }
            catch (Exception e)
            {
                output.Line("ERROR " + e.GetType().Name + ": " + e.Message);
                output.Line(e.StackTrace ?? "<no stack trace>");
            }
            finally
            {
                output.Flush();
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Refuses a command that would reconfigure or tear down a backend while a
        /// cross-frame benchmark is measuring it.  The benchmark uses the same cached
        /// backend instances as every other command, so re-initialising one mid-run
        /// would silently corrupt the measurement rather than fail loudly.
        /// </summary>
        private static bool RefuseWhileBenchDriving(ProbeOutput output, string command)
        {
            if (!Game.BenchJobDriver.IsDriving)
            {
                return true;
            }

            output.Line("refusing '" + command + "': a cross-frame benchmark is running ("
                        + Game.BenchJobDriver.DescribeQueue() + ")");
            output.Line("  it uses the same backend instances, so re-initialising one now would corrupt the measurement");
            output.Line("  use `luaprobe bench status`, `luaprobe bench cancel`, or wait for the queue to finish");
            return false;
        }

        private static void RenderHelp(ProbeOutput output)
        {
            output.Line("LuaRuntimeProbe - " + LuaProbeVersion.Value);
            output.Line("  luaprobe status                         environment + backend availability + versions");
            output.Line("  luaprobe list                           backend ids and availability");
            output.Line("  luaprobe ctx                            read-only player/world context (no writes)");
            output.Line("  luaprobe test [backend|all] [--caps]    run the functional suite");
            output.Line("  luaprobe bench [backend|all] [iters] [budgetMs]");
            output.Line("                                          bounded performance comparison");
            output.Line("  luaprobe run <backend> <lua-file> [args...]");
            output.Line("                                          execute a mod-relative Lua file");
            output.Line("  luaprobe reset                          tear down all Lua states");
            output.Line("  backends: " + LuaBackendRegistry.IdList());
        }

        private static void RenderStatus(ProbeOutput output)
        {
            output.Line("== LuaRuntimeProbe " + LuaProbeVersion.Value + " ==");
            output.Line("modDir=" + (Game.ModPaths.ResolveModDirectory() ?? "<unresolved>"));
            output.Line("luaDir=" + (ProbeHost.Current.LuaDirectory ?? "<unresolved>"));
            output.Line("nativeDir=" + (ProbeHost.Current.NativeDirectory ?? "<unresolved>"));
            output.Line("nativeLoaded=" + Backends.NativeLibraryLoader.Describe());
            output.Blank();

            output.Line("-- environment --");
            foreach (string line in EnvironmentProbe.Lines())
            {
                output.Line("  " + line);
            }

            output.Blank();
            output.Line("-- backends --");
            foreach (ILuaProbeBackend backend in LuaBackendRegistry.All)
            {
                RenderBackend(output, backend);
            }
        }

        private static void RenderBackend(ProbeOutput output, ILuaProbeBackend backend)
        {
            LuaBackendInfo info = backend.Info;
            output.Line("  [" + info.Id + "] " + info.DisplayName);
            output.Line("      available    : " + backend.IsAvailable
                        + (backend.IsAvailable ? string.Empty : " (" + backend.UnavailableReason + ")"));
            output.Line("      embed        : " + info.EmbedVersion);
            output.Line("      lua language : declared=" + info.DeclaredLuaLanguageVersion
                        + " measured=" + (info.MeasuredLuaVersion ?? "<not initialised yet>"));
            output.Line("      runtime      : " + info.RuntimeKind);
            output.Line("      native dep   : " + info.NativeDependency);
            output.Line("      source       : " + info.PackageSource);
            output.Line("      state        : " + backend.Diagnostics());
        }

        private static void RenderList(ProbeOutput output)
        {
            foreach (ILuaProbeBackend backend in LuaBackendRegistry.All)
            {
                output.Line(backend.Info.Id + "\t" + backend.IsAvailable + "\t"
                            + backend.Info.EmbedVersion + "\tlua=" + backend.Info.DeclaredLuaLanguageVersion
                            + "\t" + backend.Info.RuntimeKind);
            }
        }

        private static void RenderContext(ProbeOutput output)
        {
            PlayerSnapshot snapshot = ProbeHost.Current.CapturePlayer();
            output.Line("== read-only game context (main thread frame " + snapshot.CapturedOnFrame + ") ==");
            foreach (KeyValuePair<string, string> pair in snapshot.AsPairs())
            {
                output.Line("  " + pair.Key + " = " + (pair.Value ?? "<null>"));
            }

            output.Line("  (the host's player capture only reads; ProbeBridge exposes no mutating member)");
        }

        // ------------------------------------------------------------------

        private static void RunTest(ProbeOutput output, List<string> args)
        {
            bool includeCapabilities = false;
            string target = null;
            var ignored = new List<string>();

            foreach (string arg in args)
            {
                if (string.Equals(arg, "--caps", StringComparison.OrdinalIgnoreCase))
                {
                    includeCapabilities = true;
                }
                else if (target == null)
                {
                    target = arg;
                }
                else
                {
                    ignored.Add(arg);
                }
            }

            if (ignored.Count > 0)
            {
                output.Line("ignored unexpected argument(s): " + string.Join(" ", ignored.ToArray())
                            + " (usage: luaprobe test [backend|all] [--caps])");
            }

            List<ILuaProbeBackend> backends = SelectBackends(output, target);
            if (backends == null)
            {
                return;
            }

            foreach (ILuaProbeBackend backend in backends)
            {
                output.Blank();
                output.Line("== functional suite: " + backend.Info.Id + " (" + backend.Info.EmbedVersion + ") ==");

                ProbeSuiteResult result = ProbeSuite.Run(backend, includeCapabilities);

                foreach (ProbeCheck check in result.Checks)
                {
                    output.Line("  " + check.Render());
                }

                output.Line("  --> pass=" + result.PassedCount + " fail=" + result.FailedCount
                            + " info=" + result.InfoCount
                            + (result.Ran ? string.Empty : " (suite did not run)"));

                backend.Info.MeasuredLuaVersion = backend.Info.MeasuredLuaVersion;
                output.Line("  measured lua version: " + (backend.Info.MeasuredLuaVersion ?? "<unknown>"));
            }
        }

        private const int MaxCallbackIterations = 200000;

        /// <summary>
        /// `luaprobe bench [start] [backend|all] [iterations] [workBudgetMs]`
        /// `luaprobe bench status | cancel | step [ms] | sync [...]`
        ///
        /// The DEFAULT form starts a CROSS-FRAME job: the command returns immediately
        /// and a hidden driver advances the shared measurement kernel by a few
        /// milliseconds of measured work per frame, so no single frame is blocked for
        /// the length of the benchmark.  `sync` is the explicit opt-in for the old
        /// blocking behaviour.
        /// </summary>
        private static void RunBench(ProbeOutput output, List<string> args)
        {
            string mode = "start";

            if (args.Count > 0)
            {
                string first = args[0].ToLowerInvariant();
                if (first == "start" || first == "status" || first == "cancel" || first == "step" || first == "sync")
                {
                    mode = first;
                    args = args.GetRange(1, args.Count - 1);
                }
            }

            switch (mode)
            {
                case "status":
                    RenderBenchStatus(output);
                    return;
                case "cancel":
                    CancelBench(output);
                    return;
                case "step":
                    StepBench(output, args);
                    return;
                case "sync":
                    StartBench(output, args, true);
                    return;
                default:
                    StartBench(output, args, false);
                    return;
            }
        }

        private static void StartBench(ProbeOutput output, List<string> args, bool synchronous)
        {
            string target = null;
            int? iterations = null;
            int? budget = null;
            var ignored = new List<string>();

            foreach (string arg in args)
            {
                int parsed;
                if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    if (iterations == null)
                    {
                        iterations = parsed;
                    }
                    else if (budget == null)
                    {
                        budget = parsed;
                    }
                    else
                    {
                        ignored.Add(arg);
                    }
                }
                else if (target == null)
                {
                    target = arg;
                }
                else
                {
                    ignored.Add(arg);
                }
            }

            if (ignored.Count > 0)
            {
                output.Line("ignored unexpected argument(s): " + string.Join(" ", ignored.ToArray())
                            + " (usage: luaprobe bench [start|status|cancel|step|sync] [backend|all] [iterations] [workBudgetMs])");
            }

            List<ILuaProbeBackend> backends = SelectBackends(output, target);
            if (backends == null)
            {
                return;
            }

            if (synchronous)
            {
                if (!RefuseWhileBenchDriving(output, "bench sync"))
                {
                    return;
                }

                // The synchronous path is a deliberate opt-in; it really does block the
                // calling thread for the whole run.
                BenchConfig syncConfig = BenchConfig.Default();
                ApplyBenchOverrides(output, syncConfig, iterations, budget);

                output.Line("SYNC bench: this call blocks the main thread for the whole run"
                            + " (use `luaprobe bench` without `sync` for the cross-frame job)");

                foreach (ILuaProbeBackend backend in backends)
                {
                    BenchResult syncResult = ProbeBench.Run(backend, syncConfig);
                    RenderBenchResult(output, syncResult);
                }

                return;
            }

            if (Game.BenchJobDriver.IsDriving)
            {
                output.Line("a benchmark queue is already running: " + Game.BenchJobDriver.DescribeQueue());
                output.Line("use `luaprobe bench status` to watch it, or `luaprobe bench cancel` to stop it");
                return;
            }

            BenchConfig config = BenchConfig.ForGame();
            ApplyBenchOverrides(output, config, iterations, budget);

            string startError;
            if (!Game.BenchJobDriver.StartQueue(backends, config, out startError))
            {
                output.Line("could not start the benchmark: " + startError);
                return;
            }

            output.Line("bench job started (cross-frame): workBudgetMs=" + config.TotalWorkBudgetMs
                        + " perMetricWorkBudgetMs=" + config.PerMetricWorkBudgetMs
                        + " perFrameWorkBudgetMs=" + config.PerAdvanceWorkBudgetMs
                        + " backends=" + string.Join(",", Game.BenchJobDriver.QueuedBackends));
            output.Line("  the job advances a few ms of MEASURED work per frame and never blocks a frame for the whole run");
            output.Line("  backend setup (init + module load) happens on the frame that starts each backend and is"
                        + " reported as setup= in the result; see 'luaprobe bench status'");
            output.Line("  poll with `luaprobe bench status`; stop with `luaprobe bench cancel`");
            output.Line("  active_work counts real work; idle is waiting between frames; overhead is setup/verify/GC");
        }

        private static void ApplyBenchOverrides(ProbeOutput output, BenchConfig config, int? iterations, int? budget)
        {
            if (iterations.HasValue && iterations.Value > 0)
            {
                config.HotIterations = iterations.Value;
                config.BridgeDirectIterations = iterations.Value;
                config.DtoIterations = Math.Max(1, iterations.Value / 5);
                config.CallbackIterations = Math.Max(1, iterations.Value / 5);
            }
            else if (iterations.HasValue)
            {
                output.Line("iterations must be positive; using the defaults");
            }

            // Hard ceiling: a single Lua call cannot be interrupted, so the callback
            // metric is chunked and its total is capped to keep main-thread blocking
            // time bounded.
            if (config.CallbackIterations > MaxCallbackIterations)
            {
                output.Line("callback iterations capped at " + MaxCallbackIterations
                            + " (requested " + config.CallbackIterations + ")");
                config.CallbackIterations = MaxCallbackIterations;
            }

            if (budget.HasValue && budget.Value > 0)
            {
                config.TotalWorkBudgetMs = budget.Value;
                config.PerMetricWorkBudgetMs = Math.Max(50, budget.Value / 4);
            }
            else if (budget.HasValue)
            {
                output.Line("workBudgetMs must be positive; using the default");
            }

            output.Line("bench config: hotIterations=" + config.HotIterations
                        + " dtoIterations=" + config.DtoIterations
                        + " callbackIterations=" + config.CallbackIterations
                        + " callbackChunk=" + config.CallbackChunk
                        + " compileIterations=" + config.CompileIterations
                        + " coldInitIterations=" + config.ColdInitIterations
                        + " totalWorkBudgetMs=" + config.TotalWorkBudgetMs
                        + " perMetricWorkBudgetMs=" + config.PerMetricWorkBudgetMs
                        + " cleanBaseline=" + config.CleanBaselineBeforeEachRow);
        }

        private static void RenderBenchStatus(ProbeOutput output)
        {
            BenchJob job = Game.BenchJobDriver.CurrentJob;
            IList<BenchResult> results = Game.BenchJobDriver.CompletedResults;

            if (job == null && results.Count == 0)
            {
                output.Line("no benchmark has been run in this game session");
                output.Line("start one with: luaprobe bench [backend|all] [iterations] [workBudgetMs]");
                return;
            }

            output.Line("bench queue: " + Game.BenchJobDriver.DescribeQueue());

            foreach (string note in Game.BenchJobDriver.QueueNotes)
            {
                output.Line("  note: " + note);
            }

            if (job != null)
            {
                output.Line("  " + job.Describe());
                output.Line("  active_ms counts measured work only; idle_ms is wall time spent waiting between frames."
                            + " Per-op figures use active work, so a cross-frame run is not penalised for waiting.");
            }

            foreach (BenchResult result in results)
            {
                RenderBenchResult(output, result);
            }

            if (job != null && job.Status == BenchJobStatus.Running && results.Count == 0)
            {
                output.Line("  (no backend has finished yet)");
            }
        }

        private static void CancelBench(ProbeOutput output)
        {
            if (!Game.BenchJobDriver.IsDriving)
            {
                output.Line("no benchmark queue is running");
                return;
            }

            Game.BenchJobDriver.CancelQueue();
            output.Line("cancellation requested; the current chunk finishes, then the queue stops");
            output.Line("check with `luaprobe bench status`");
        }

        private static void StepBench(ProbeOutput output, List<string> args)
        {
            double budget = 50;

            if (args.Count > 0)
            {
                int parsed;
                if (int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
                {
                    budget = parsed;
                }
                else
                {
                    output.Line("usage: luaprobe bench step [workBudgetMs]  (positive integer)");
                    return;
                }
            }

            string detail;
            bool finished = Game.BenchJobDriver.AdvanceByStep(budget, out detail);
            output.Line("advanced by up to " + F(budget) + "ms of measured work; finished=" + finished);
            output.Line("  " + (detail ?? "<no detail>"));
            output.Line("  (normally the per-frame driver does this automatically; `step` exists so the job can be"
                        + " driven deterministically, or when no driver is active)");
        }

        private static void RenderBenchResult(ProbeOutput output, BenchResult result)
        {
            ILuaProbeBackend backend = LuaBackendRegistry.Find(result.BackendId);

            output.Blank();
            output.Line("== benchmark: " + result.BackendId
                        + (backend != null ? " (" + backend.Info.EmbedVersion + ", declared lua "
                                             + backend.Info.DeclaredLuaLanguageVersion + ")" : string.Empty)
                        + " ==");

            if (!string.IsNullOrEmpty(result.Error))
            {
                output.Line("  UNRUNNABLE: " + result.Error);
                return;
            }

            output.Line("  status=" + result.Status
                        + " measured lua version=" + (backend != null
                            ? (backend.Info.MeasuredLuaVersion ?? "<unknown>")
                            : "<unknown>"));
            output.Line("  active_work=" + F(result.ActiveMs) + "ms  wall=" + F(result.WallMs)
                        + "ms  idle(waiting between advances)=" + F(result.IdleMs)
                        + "ms  overhead(setup/verify/GC inside advances)=" + F(result.OverheadMs)
                        + "ms  advances=" + result.AdvanceCount
                        + "  setup=" + F(result.SetupMs) + "ms");
            output.Line("  setup is the backend initialise + module load + bridge install; it runs on the frame that"
                        + " starts this backend and is NOT bounded by the per-frame work budget");
            output.Line("  metric                          iters(req/performed/warm)  active_ms  wall_ms   us/op    gen0/1/2   allocated  managed_delta  checksum  ok  flag");

            foreach (BenchRow row in result.Rows)
            {
                output.Line("  " + Pad(row.Metric, 31)
                            + Pad(row.RequestedIterations + "/" + row.PerformedIterations + "/" + row.WarmupIterations, 27)
                            + Pad(F(row.ActiveMs), 10)
                            + Pad(F(row.WallMs), 10)
                            + Pad(F(row.PerOpMicros), 9)
                            + Pad(row.Gen0 + "/" + row.Gen1 + "/" + row.Gen2, 11)
                            + Pad(row.AllocatedBytes < 0 ? "n/a" : row.AllocatedBytes.ToString(CultureInfo.InvariantCulture), 11)
                            + Pad(row.ManagedDelta.ToString(CultureInfo.InvariantCulture), 15)
                            + Pad(row.Checksum, 10)
                            + Pad(row.ChecksumOk ? "Y" : "N", 4)
                            + row.Flag);

                if (!string.IsNullOrEmpty(row.Notes))
                {
                    output.Line("      note: " + row.Notes);
                }

                if (!row.IsVerifiable || !row.ChecksumOk)
                {
                    output.Line("      NOT VERIFIED: " + (row.AbortReason ?? "checksum mismatch")
                                + "; expected checksum " + (row.ExpectedChecksum ?? "n/a"));
                }
            }

            output.Line("  flag census: " + result.FlagCensus());
            if (!result.AllRowsPresent)
            {
                output.Line("  WARNING: the kernel produced " + result.Rows.Count + " row(s) for "
                            + result.ExpectedRowCount + " requested metric(s); the table is incomplete");
            }
            output.Line("  expected checksums: " + ExpectedSummary(result));
            output.Line("  flags: - measured, T truncated at the work budget, C cancelled (partial row, still"
                        + " verified against the iterations it did), S skipped (budget already spent), A aborted");
            output.Line("  managed_delta is a live heap delta, not an allocation count, and may be negative; "
                        + "allocated=n/a means GC.GetTotalAllocatedBytes is unavailable on this runtime "
                        + "(never reported as 0).");
        }

        private static string ExpectedSummary(BenchResult result)
        {
            var parts = new List<string>();
            foreach (BenchRow row in result.Rows)
            {
                parts.Add(row.Metric + " => " + (row.ExpectedChecksum ?? "n/a"));
            }

            return string.Join(" | ", parts.ToArray());
        }

        private static void RunScript(ProbeOutput output, List<string> args)
        {
            if (args.Count < 2)
            {
                output.Line("usage: luaprobe run <backend> <mod-relative-lua-path> [args...]");
                return;
            }

            ILuaProbeBackend backend = LuaBackendRegistry.Find(args[0]);
            if (backend == null)
            {
                output.Line("unknown backend '" + args[0] + "'; known: " + LuaBackendRegistry.IdList());
                return;
            }

            if (!backend.IsAvailable)
            {
                output.Line("backend '" + backend.Info.Id + "' is unavailable: " + backend.UnavailableReason);
                return;
            }

            string scriptPath = args[1];
            var scriptArgs = new List<object>();
            for (int i = 2; i < args.Count; i++)
            {
                scriptArgs.Add(args[i]);
            }

            string error;
            if (!backend.Initialize(out error))
            {
                output.Line("initialize failed: " + error);
                return;
            }

            if (!backend.LoadScriptFile("Lua/probe_helpers.lua", ProbeSuite.HelperModule, out error))
            {
                output.Line("helper load failed: " + error);
                return;
            }

            if (!backend.LoadScriptFile(scriptPath, "user_module", out error))
            {
                output.Line("script load failed: " + error);
                return;
            }

            LuaValue result = backend.CallFunction("user_module", "main", scriptArgs.ToArray(), out error);
            if (!string.IsNullOrEmpty(error))
            {
                output.Line("script '" + scriptPath + "' loaded, but user_module.main(...) could not be called: " + error);
                output.Line("  (a script run this way must return a table containing a function `main`)");
                return;
            }

            output.Line("user_module.main returned " + result.Display + " (" + result.KindName + ")");
        }

        // ------------------------------------------------------------------

        private static List<ILuaProbeBackend> SelectBackends(ProbeOutput output, string target)
        {
            var selected = new List<ILuaProbeBackend>();

            if (string.IsNullOrEmpty(target) || string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (ILuaProbeBackend backend in LuaBackendRegistry.All)
                {
                    selected.Add(backend);
                }

                return selected;
            }

            ILuaProbeBackend single = LuaBackendRegistry.Find(target);
            if (single == null)
            {
                output.Line("unknown backend '" + target + "'; known: " + LuaBackendRegistry.IdList());
                return null;
            }

            selected.Add(single);
            return selected;
        }

        private static string Pad(string text, int width)
        {
            string value = text ?? string.Empty;
            if (value.Length >= width)
            {
                return value + " ";
            }

            return value + new string(' ', width - value.Length);
        }

        private static string F(double value)
        {
            if (double.IsNaN(value))
            {
                return "n/a";
            }

            return value.ToString(value >= 1000 ? "F0" : "F3", CultureInfo.InvariantCulture);
        }
    }
}
