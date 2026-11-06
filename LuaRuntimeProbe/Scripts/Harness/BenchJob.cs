using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using LuaRuntimeProbe.Backends;
using LuaRuntimeProbe.Contracts;

namespace LuaRuntimeProbe.Harness
{
    /// <summary>One measured metric, as produced by the kernel.</summary>
    public sealed class BenchRow
    {
        public string Metric;
        public string Unit = "op";
        public int RequestedIterations;
        public int PerformedIterations;
        public int WarmupIterations;

        /// <summary>
        /// Time actually spent doing the work of this row, summed over the steps.
        /// This - not the wall time - is what the budget is charged against.
        /// </summary>
        public double ActiveMs;

        /// <summary>
        /// The part of <see cref="ActiveMs"/> that belongs to measured iterations
        /// (warm-up excluded).  Per-op figures are derived from THIS, so warm-up can
        /// never inflate them while still being charged to the budget.
        /// </summary>
        public double MeasuredMs;

        /// <summary>Wall time from the row's first step to its last, including any cross-frame gaps.</summary>
        public double WallMs;

        /// <summary>Number of bounded steps this row was executed in.</summary>
        public int Steps;

        public double PerOpMicros;
        public int Gen0;
        public int Gen1;
        public int Gen2;

        /// <summary>-1 means "not measurable on this runtime"; never reported as 0.</summary>
        public long AllocatedBytes = -1;

        public long ManagedDelta;
        public string Checksum;
        public string ExpectedChecksum;
        public bool ChecksumOk;

        /// <summary>Running checksum accumulated by the kernel; rendered into <see cref="Checksum"/>.</summary>
        public long ChecksumValue;

        /// <summary>Baselines captured by the kernel when the row started.</summary>
        public long AllocatedBefore = -1;

        public long ManagedBefore;

        /// <summary>True when the work budget stopped the row early.</summary>
        public bool Truncated;

        /// <summary>True when the operator cancelled while this row was in flight.</summary>
        public bool Cancelled;

        /// <summary>True when the row was never measured (budget already spent).</summary>
        public bool Skipped;

        /// <summary>True when the row threw or the operation failed.</summary>
        public bool Aborted;

        /// <summary>Expected checksum as a number, for metrics that are not "coefficient * iterations".</summary>
        public long ExpectedChecksumValue = long.MinValue;

        /// <summary>Set by the callback metric: the bridge counters matched the expectation.</summary>
        public bool DeliveryVerified;

        public string AbortReason;
        public string Notes;

        /// <summary>A row may only be reported as verified if it really completed work.</summary>
        public bool IsVerifiable
        {
            get { return !Skipped && !Aborted && PerformedIterations > 0; }
        }

        /// <summary>Single-character flag used by every report: - T C S A.</summary>
        public string Flag
        {
            get
            {
                if (Skipped)
                {
                    return "S";
                }

                if (Aborted)
                {
                    return "A";
                }

                if (Cancelled)
                {
                    return "C";
                }

                return Truncated ? "T" : "-";
            }
        }
    }

    public sealed class BenchConfig
    {
        /// <summary>
        /// Soft budget for the whole run, expressed in MEASURED WORK milliseconds -
        /// not wall time.  A cross-frame run can therefore take much longer in wall
        /// time than this number; that gap is reported separately as idle time.
        /// </summary>
        public int TotalWorkBudgetMs = 3000;

        /// <summary>Soft budget for a single metric, in measured work milliseconds.</summary>
        public int PerMetricWorkBudgetMs = 750;

        /// <summary>
        /// Measured work allowed per Advance() call.  The in-game driver sets this to
        /// a few milliseconds so one frame is never blocked for long; the synchronous
        /// offline driver uses the per-metric budget.
        /// </summary>
        public int PerAdvanceWorkBudgetMs = 750;

        public int HotIterations = 100000;
        public int DtoIterations = 10000;
        public int CallbackIterations = 20000;
        public int CompileIterations = 50;
        public int ColdInitIterations = 10;
        public int BridgeDirectIterations = 20000;

        /// <summary>
        /// Crossings per Lua call in the boundary metric.  A single Lua call cannot be
        /// interrupted, so this is the granularity at which that metric can be bounded.
        /// </summary>
        public int CallbackChunk = 2000;

        /// <summary>
        /// Forces a full GC between rows.  Good for measurement stability offline;
        /// deliberately OFF for the in-game driver, where a blocking collection on the
        /// main thread would be a visible hitch.  With it off, managed deltas are
        /// noisier - which the report says.
        /// </summary>
        public bool CleanBaselineBeforeEachRow = true;

        /// <summary>Number of loop iterations per step; the kernel's chunk size.</summary>
        public int LoopChunk = 512;

        public static BenchConfig Default()
        {
            return new BenchConfig();
        }

        public static BenchConfig ForGame()
        {
            BenchConfig config = Default();
            config.CleanBaselineBeforeEachRow = false;
            config.PerAdvanceWorkBudgetMs = 4;
            return config;
        }
    }

    public enum BenchJobStatus
    {
        Running = 0,
        Completed = 1,
        Cancelled = 2,
        Failed = 3,
    }

    public sealed class BenchResult
    {
        public string BackendId;
        public int TotalWorkBudgetMs;
        public BenchJobStatus Status = BenchJobStatus.Completed;

        /// <summary>Measured work time.</summary>
        public double ActiveMs;

        /// <summary>Wall time, including the gaps between frames.</summary>
        public double WallMs;

        /// <summary>Time inside Advance() calls: measured steps plus setup/verify/GC overhead.</summary>
        public double AdvanceBusyMs;

        /// <summary>Advance time that was not a measured step (GC, Begin/Finish/verify, bookkeeping).</summary>
        public double OverheadMs;

        /// <summary>Time the job spent waiting between advances (cross-frame waiting).</summary>
        public double IdleMs;

        /// <summary>
        /// Time to initialise the backend, load the probe modules and install the bridge.
        /// This runs on the calling thread and is NOT bounded by the per-frame work
        /// budget, so it is reported rather than hidden.
        /// </summary>
        public double SetupMs;

        public int AdvanceCount;
        public readonly List<BenchRow> Rows = new();
        public string Error;

        /// <summary>
        /// Number of metrics the kernel was asked to measure.  Reported alongside the
        /// row count so a kernel bug that silently drops metrics cannot look like a
        /// clean run.
        /// </summary>
        public int ExpectedRowCount;

        /// <summary>True when every requested metric produced a row.</summary>
        public bool AllRowsPresent
        {
            get { return ExpectedRowCount == 0 || Rows.Count == ExpectedRowCount; }
        }

        /// <summary>Count of rows per flag, so a report cannot silently disagree with the table.</summary>
        public string FlagCensus()
        {
            int measured = 0;
            int truncated = 0;
            int cancelled = 0;
            int skipped = 0;
            int aborted = 0;
            int unverified = 0;

            foreach (BenchRow row in Rows)
            {
                switch (row.Flag)
                {
                    case "-":
                        measured++;
                        break;
                    case "T":
                        truncated++;
                        break;
                    case "C":
                        cancelled++;
                        break;
                    case "S":
                        skipped++;
                        break;
                    case "A":
                        aborted++;
                        break;
                }

                if (!row.IsVerifiable || !row.ChecksumOk)
                {
                    unverified++;
                }
            }

            return "rows=" + Rows.Count + "/" + ExpectedRowCount + " measured=" + measured + " truncated=" + truncated
                   + " cancelled=" + cancelled + " skipped=" + skipped + " aborted=" + aborted
                   + " unverified=" + unverified;
        }
    }

    /// <summary>Outcome of one bounded step.</summary>
    internal struct BenchStep
    {
        /// <summary>True when the task has no more steps.</summary>
        public bool Done;

        /// <summary>True when this step was warm-up rather than measured work.</summary>
        public bool WasWarmup;

        public static BenchStep More(bool warmup)
        {
            return new BenchStep { Done = false, WasWarmup = warmup };
        }

        public static BenchStep Finished(bool warmup)
        {
            return new BenchStep { Done = true, WasWarmup = warmup };
        }
    }

    /// <summary>
    /// One bounded unit of measurable work.  Step() must return after a bounded amount
    /// of work so that the caller can decide whether to keep going (offline) or yield
    /// until the next frame (in-game).
    /// </summary>
    internal sealed class BenchTask
    {
        public string Metric;
        public string Unit = "op";
        public int RequestedIterations;
        public int WarmupIterations;
        public string ExpectedExpression;

        /// <summary>
        /// Runs once, immediately before this task's first step.  Used for state that
        /// must be prepared at task START rather than at job construction - e.g. the
        /// callback metric resets the shared bridge here, because an earlier row
        /// (csharp_baseline.bridge_direct) deliberately calls the same bridge and would
        /// otherwise be counted as delivered crossings.
        /// </summary>
        public Action<BenchRow> Begin;

        public Func<BenchRow, BenchStep> Step;
        public Action<BenchRow> Finish;
        public readonly BenchRow Row = new();
    }

    /// <summary>
    /// The shared measurement kernel.
    ///
    /// It owns the task list, the accumulators and the verification, and exposes a
    /// single entry point - Advance(workBudgetMs) - that performs a BOUNDED amount of
    /// measured work and then yields.  Both drivers use it unchanged:
    ///   * ProbeBench.Run  advances in a loop, offline, synchronously;
    ///   * the in-game driver advances once per frame.
    ///
    /// BUDGETING is in measured work milliseconds, never wall time, so a cross-frame
    /// run is not penalised for the time it spent waiting and the budget stays a real
    /// bound on main-thread work per Advance() call.
    /// </summary>
    public sealed class BenchJob
    {
        private readonly ILuaProbeBackend _backend;
        private readonly BenchConfig _config;
        private readonly List<BenchTask> _tasks = new();
        private readonly List<BenchRow> _completed = new();

        private int _index = -1;
        private BenchTask _current;
        private long _rowWallStartTicks;
        private double _activeMs;
        private double _advanceBusyMs;
        private int _advanceCount;
        private bool _cancelRequested;

        private readonly Stopwatch _wall = new();

        private BenchJob(ILuaProbeBackend backend, BenchConfig config)
        {
            _backend = backend;
            _config = config;
        }

        /// <summary>
        /// Setup cost (backend initialise + module load + bridge install) measured by
        /// Create().  This happens on the calling thread and is NOT bounded by the
        /// per-frame work budget, so it is reported instead of being hidden.
        /// </summary>
        public double SetupMs { get; private set; }

        public BenchJobStatus Status { get; private set; } = BenchJobStatus.Running;

        public string Error { get; private set; }

        public string BackendId
        {
            get { return _backend != null ? _backend.Info.Id : "<none>"; }
        }

        public IList<BenchRow> CompletedRows
        {
            get { return _completed; }
        }

        public string CurrentMetric
        {
            get
            {
                if (_current != null)
                {
                    return _current.Metric;
                }

                return _index + 1 < _tasks.Count ? _tasks[_index + 1].Metric : "<none>";
            }
        }

        public int RemainingTaskCount
        {
            get { return Math.Max(0, _tasks.Count - (_index + 1)); }
        }

        /// <summary>Number of rows finished so far.</summary>
        public int CompletedTaskCount
        {
            get { return _completed.Count; }
        }

        public int TotalTaskCount
        {
            get { return _tasks.Count; }
        }

        /// <summary>Measured work performed so far (the sum of step times).</summary>
        public double ActiveMs
        {
            get { return _activeMs; }
        }

        /// <summary>
        /// Total time spent inside Advance() calls - measured steps PLUS the setup,
        /// verification, GC and bookkeeping that happens between them.  This, not
        /// ActiveMs, is what separates real work from waiting.
        /// </summary>
        public double AdvanceBusyMs
        {
            get { return _advanceBusyMs; }
        }

        /// <summary>Work that is not a measured step: GC, Begin/Finish/verify, bookkeeping.</summary>
        public double OverheadMs
        {
            get { return Math.Max(0.0, AdvanceBusyMs - ActiveMs); }
        }

        public double WallMs
        {
            get { return _wall.Elapsed.TotalMilliseconds; }
        }

        /// <summary>
        /// Wall time not spent inside Advance() - i.e. time the job was waiting for the
        /// next frame (cross-frame waiting), NOT "all non-step time".
        /// </summary>
        public double IdleMs
        {
            get { return Math.Max(0.0, WallMs - AdvanceBusyMs); }
        }

        public int AdvanceCount
        {
            get { return _advanceCount; }
        }

        /// <summary>
        /// Builds a job.  The backend must already be loaded with both probe modules;
        /// the job does that itself and reports failure through <paramref name="error"/>.
        /// </summary>
        public static BenchJob Create(ILuaProbeBackend backend, BenchConfig config, out string error)
        {
            var setupWatch = Stopwatch.StartNew();
            error = null;

            if (backend == null)
            {
                error = "no backend";
                return null;
            }

            if (!backend.IsAvailable)
            {
                error = "backend unavailable: " + backend.UnavailableReason;
                return null;
            }

            if (!backend.Initialize(out error))
            {
                error = "initialize failed: " + error;
                return null;
            }

            string loadError = null;
            if (!backend.LoadScriptFile("Lua/probe_helpers.lua", ProbeSuite.HelperModule, out loadError)
                || !backend.LoadScriptFile("Lua/probe_core.lua", ProbeSuite.CoreModule, out loadError))
            {
                error = "module load failed: " + loadError;
                return null;
            }

            string sourceError;
            string coreSource = ProbeHost.Current.ReadModFile("Lua/probe_core.lua", out sourceError);
            if (coreSource == null)
            {
                error = "could not read Lua/probe_core.lua: " + sourceError;
                return null;
            }

            backend.InvalidatePlayerSnapshot();
            var bridge = new ProbeBridge(ProbeHost.Current.CapturePlayer);
            var host = backend as IProbeBridgeHost;
            if (host != null)
            {
                host.SetBridge(bridge);
            }

            ProbeContextDto standard = ProbeContextDto.CreateStandard();
            long sumIdsPerIteration = ProbeSuite.ExpectedSumIds(standard);

            var job = new BenchJob(backend, config);

            job.AddLoopTask("csharp_baseline.add", "call", config.HotIterations, 1000, "5 * iterations",
                "minimal-work reference: a constant return in C#; NOT an upper bound for a Lua call",
                _ => 2 + 3);

            job.AddLoopTask("csharp_baseline.build_dto", "build", config.DtoIterations, 200, "3 * iterations",
                "CLR object-graph construction only; the Lua DTO rows additionally build a Lua table",
                _ => ProbeContextDto.CreateStandard().Events.Length);

            job.AddLoopTask("csharp_baseline.bridge_direct", "call", config.BridgeDirectIterations, 200, "iterations",
                "direct CLR call to the same method Lua reaches through the binding layer",
                _ =>
                {
                    bridge.Emit(1);
                    return 1;
                });

            job.AddColdInitTask(backend.Info.Id, config);

            var compileCounter = new int[1];
            job.AddLoopTask("lua_compile_core", "compile", config.CompileIterations, 2,
                coreSource.Length + " * iterations",
                "compile + execute probe_core.lua as a fresh chunk each time",
                _ =>
                {
                    string global = "__lrp_compile_" + compileCounter[0]++;
                    string error2;
                    bool ok = backend.LoadScriptText(coreSource, "Lua/probe_core.lua", global, out error2);
                    if (!ok)
                    {
                        throw new InvalidOperationException("LoadScriptText failed: " + error2);
                    }

                    return coreSource.Length;
                });

            job.AddLoopTask("lua_hot_call.add", "call", config.HotIterations, 2000, "5 * iterations",
                "resolved function handle + primitive args + return conversion (no table marshalling); "
                + "every backend caches the resolved handle, so no per-iteration global lookup is included",
                _ => LuaCallLong(backend, "add", new object[] { 2, 3 }, expected: 5));

            job.AddLoopTask("lua_dto_push_touch", "push", config.DtoIterations, 100, "2 * iterations",
                "end-to-end adapter cost of building the nested context table from a C# DTO and reading one field. "
                + "The per-table construction strategy differs per adapter (see the backend notes), so this row "
                + "compares adapter implementations, not the Lua engines.",
                _ => LuaCallLong(backend, "touch", new object[] { standard }, expected: 2));

            job.AddLoopTask("lua_dto_sum_ids", "call", config.DtoIterations, 100, sumIdsPerIteration + " * iterations",
                "push + full nested traversal; checksum cross-checked against the C# re-implementation",
                _ => LuaCallLong(backend, "sum_ids", new object[] { standard }, expected: sumIdsPerIteration));

            job.AddLoopTask("lua_dto_evaluate", "call", config.DtoIterations, 100, "iterations",
                "push + traversal + rules; the checksum counts only iterations that actually returned \"Engage\"",
                _ =>
                {
                    string error2;
                    LuaValue v = backend.CallFunction(ProbeSuite.CoreModule, "evaluate", new object[] { standard }, out error2);
                    if (!string.IsNullOrEmpty(error2))
                    {
                        throw new InvalidOperationException(error2);
                    }

                    return v != null && v.IsString && v.Text == "Engage" ? 1L : 0L;
                });

            job.AddCallbackTask(bridge, config);

            setupWatch.Stop();
            job.SetupMs = setupWatch.Elapsed.TotalMilliseconds;
            return job;
        }

        private static long LuaCallLong(ILuaProbeBackend backend, string function, object[] args, long expected)
        {
            string error;
            LuaValue value = backend.CallFunction(ProbeSuite.CoreModule, function, args, out error);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(function + " failed: " + error);
            }

            if (value == null || !value.IsNumber)
            {
                throw new InvalidOperationException(function + " returned " + (value == null ? "null" : value.Display));
            }

            long actual = (long)value.Number;
            if (actual != expected)
            {
                throw new InvalidOperationException(function + " returned " + actual + ", expected " + expected);
            }

            return actual;
        }

        // ------------------------------------------------------------------
        // task construction
        // ------------------------------------------------------------------

        private void AddLoopTask(string metric, string unit, int requested, int warmup, string expectedExpression,
            string notes, Func<long, long> body)
        {
            int chunk = Math.Max(1, Math.Min(_config.LoopChunk, Math.Max(1, requested / 16 + 1)));
            var state = new LoopState();

            _tasks.Add(new BenchTask
            {
                Metric = metric,
                Unit = unit,
                RequestedIterations = requested,
                WarmupIterations = warmup,
                ExpectedExpression = expectedExpression,
                Step = row =>
                {
                    if (!state.WarmedUp)
                    {
                        int warmupLeft = Math.Max(0, warmup - state.WarmupDone);
                        if (warmupLeft > 0)
                        {
                            int n = Math.Min(chunk, warmupLeft);
                            for (int i = 0; i < n; i++)
                            {
                                body(state.WarmupDone + i);
                            }

                            state.WarmupDone += n;
                        }

                        if (state.WarmupDone < warmup)
                        {
                            return BenchStep.More(true);
                        }

                        state.WarmedUp = true;

                        // The final warm-up chunk is returned as its own step.  Folding it
                        // into the first measured step charged warm-up time to the per-op
                        // figures, which is exactly what MeasuredMs exists to prevent.
                        return BenchStep.More(true);
                    }

                    if (state.Performed >= requested)
                    {
                        return BenchStep.Finished(false);
                    }

                    int take = Math.Min(chunk, requested - state.Performed);
                    for (int i = 0; i < take; i++)
                    {
                        row.ChecksumValue += body(state.Performed + i);
                    }

                    state.Performed += take;
                    row.PerformedIterations = state.Performed;
                    return state.Performed >= requested ? BenchStep.Finished(false) : BenchStep.More(false);
                },
                Finish = row =>
                {
                    row.Truncated = state.Performed < requested;
                    if (state.Performed < requested)
                    {
                        row.Notes = AppendNote(row.Notes, "stopped by the work budget");
                    }
                },
            });

            _tasks[_tasks.Count - 1].Row.Notes = notes;
        }

        private void AddColdInitTask(string backendId, BenchConfig config)
        {
            int done = 0;

            _tasks.Add(new BenchTask
            {
                Metric = "lua_cold_init",
                Unit = "init",
                RequestedIterations = config.ColdInitIterations,
                WarmupIterations = 0,
                ExpectedExpression = "2 * iterations",
                Step = row =>
                {
                    if (done >= config.ColdInitIterations)
                    {
                        return BenchStep.Finished(false);
                    }

                    ILuaProbeBackend fresh = LuaBackendRegistry.CreateNew(backendId);
                    if (fresh == null)
                    {
                        throw new InvalidOperationException("CreateNew(" + backendId + ") returned null");
                    }

                    try
                    {
                        string error;
                        if (!fresh.Initialize(out error))
                        {
                            throw new InvalidOperationException("init failed: " + error);
                        }

                        if (!fresh.LoadScriptFile("Lua/probe_helpers.lua", ProbeSuite.HelperModule, out error)
                            || !fresh.LoadScriptFile("Lua/probe_core.lua", ProbeSuite.CoreModule, out error))
                        {
                            throw new InvalidOperationException("module load failed: " + error);
                        }

                        string callError;
                        LuaValue value = fresh.CallFunction(ProbeSuite.CoreModule, "add", new object[] { 1, 1 }, out callError);
                        if (value == null || !value.IsNumber)
                        {
                            throw new InvalidOperationException("add(1,1) failed: " + callError);
                        }

                        row.ChecksumValue += (long)value.Number;
                    }
                    finally
                    {
                        try
                        {
                            fresh.Dispose();
                        }
                        catch (Exception disposeError)
                        {
                            row.Notes = AppendNote(row.Notes, "dispose threw " + disposeError.GetType().Name);
                        }
                    }

                    done++;
                    row.PerformedIterations = done;
                    return done >= config.ColdInitIterations ? BenchStep.Finished(false) : BenchStep.More(false);
                },
                Finish = row =>
                {
                    row.Truncated = done < config.ColdInitIterations;
                },
            });

            _tasks[_tasks.Count - 1].Row.Notes =
                "fresh interpreter + probe_helpers.lua + probe_core.lua + add(1,1) + shutdown";
        }

        private void AddCallbackTask(ProbeBridge bridge, BenchConfig config)
        {
            int performed = 0;
            long expectedSum = 0;
            int chunk = Math.Max(1, config.CallbackChunk);

            _tasks.Add(new BenchTask
            {
                Metric = "lua_to_csharp.boundary",
                Unit = "callback",
                RequestedIterations = config.CallbackIterations,
                WarmupIterations = 0,
                ExpectedExpression = "<chunked triangles>",
                Begin = _ =>
                {
                    // Reset at task START: csharp_baseline.bridge_direct calls the same
                    // bridge, so resetting at construction time left its calls counted as
                    // delivered crossings.
                    bridge.Reset();
                },
                Step = row =>
                {
                    if (performed >= config.CallbackIterations)
                    {
                        return BenchStep.Finished(false);
                    }

                    int n = Math.Min(chunk, config.CallbackIterations - performed);

                    string error;
                    LuaValue value = _backend.CallFunction(
                        ProbeSuite.CoreModule, "callback_probe", new object[] { n }, out error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new InvalidOperationException("callback_probe(" + n + ") failed: " + error);
                    }

                    if (value == null || !value.IsNumber)
                    {
                        throw new InvalidOperationException("callback_probe(" + n + ") returned "
                                                            + (value == null ? "null" : value.Display));
                    }

                    row.ChecksumValue += (long)value.Number;
                    expectedSum += Triangle(n);
                    performed += n;
                    row.PerformedIterations = performed;
                    row.ExpectedChecksumValue = expectedSum;
                    row.DeliveryVerified = bridge.EmitCount == performed && bridge.EmitSum == expectedSum;

                    return performed >= config.CallbackIterations ? BenchStep.Finished(false) : BenchStep.More(false);
                },
                Finish = row =>
                {
                    row.Truncated = performed < config.CallbackIterations;
                    row.Notes = AppendNote(row.Notes,
                        "emitCount=" + bridge.EmitCount + " emitSum=" + bridge.EmitSum
                        + " (expected emitCount=" + performed + " emitSum=" + expectedSum + ")");
                },
            });

            _tasks[_tasks.Count - 1].Row.Notes =
                "Lua -> C# crossings in chunks of " + chunk
                + "; chunking is what bounds how long a single Lua call can block the main thread, and each chunk"
                + " restarts the emitted id sequence, so the expected sum is the sum of per-chunk triangles";
        }

        private static long Triangle(long n)
        {
            return n * (n + 1) / 2;
        }

        private static string AppendNote(string existing, string addition)
        {
            return string.IsNullOrEmpty(existing) ? addition : existing + "; " + addition;
        }

        private sealed class LoopState
        {
            public int Performed;
            public int WarmupDone;
            public bool WarmedUp;
        }

        // ------------------------------------------------------------------
        // driving
        // ------------------------------------------------------------------

        /// <summary>
        /// Performs at most <paramref name="workBudgetMs"/> of MEASURED work.
        /// Returns true when the job is no longer running.
        /// </summary>
        public bool Advance(double workBudgetMs)
        {
            if (Status != BenchJobStatus.Running)
            {
                return true;
            }

            if (!_wall.IsRunning)
            {
                _wall.Start();
            }

            _advanceCount++;
            double spentThisAdvance = 0;
            var advanceWatch = Stopwatch.StartNew();

            try
            {
                while (Status == BenchJobStatus.Running)
                {
                    if (_cancelRequested)
                    {
                        // A cancelled row is a PARTIAL MEASUREMENT, not a placeholder:
                        // finish its timing, checksum and verification so the report can
                        // distinguish "cancelled here" from "never ran".
                        //
                        // _current can legitimately be null here (cancel before the first
                        // step, or between two metrics) - in that case there is simply no
                        // partial row to finish, and cancelling must still succeed rather
                        // than dereferencing null.
                        if (_current != null)
                        {
                            FinishCurrentEarly("cancelled by the operator", cancelled: true);
                        }

                        Status = BenchJobStatus.Cancelled;
                        break;
                    }

                    if (_current == null)
                    {
                        if (!MoveToNextTask())
                        {
                            Status = BenchJobStatus.Completed;
                            break;
                        }
                    }

                    if (_current.Row.Skipped)
                    {
                        // A skipped task never ran, so its Finish() is intentionally NOT
                        // invoked: Finish would attach per-task observations (delivered
                        // crossings, "stopped by budget") to a row with no measurement.
                        CompleteRow(_current.Row);
                        _completed.Add(_current.Row);
                        _current = null;
                        continue;
                    }

                    BenchStep step = default(BenchStep);
                    double stepMs = 0;
                    var stepWatch = Stopwatch.StartNew();
                    try
                    {
                        step = _current.Step(_current.Row);
                    }
                    finally
                    {
                        // Charged in a finally so an aborting step is still timed.
                        stepWatch.Stop();
                        stepMs = stepWatch.Elapsed.TotalMilliseconds;
                        _current.Row.ActiveMs += stepMs;
                        if (!step.WasWarmup)
                        {
                            _current.Row.MeasuredMs += stepMs;
                        }

                        _current.Row.Steps++;
                        _activeMs += stepMs;
                        spentThisAdvance += stepMs;
                    }

                    if (step.Done)
                    {
                        CompleteCurrentTask();
                        if (spentThisAdvance >= workBudgetMs)
                        {
                            break;
                        }

                        continue;
                    }

                    // Per-metric AND total caps are re-checked at every step boundary.
                    // Without the per-metric check a metric simply ran to completion no
                    // matter how small the budget was, which made the "truncated" flag
                    // unreachable and the budget a lie for every individual row.
                    if (_config.PerMetricWorkBudgetMs > 0
                        && _current.Row.ActiveMs >= _config.PerMetricWorkBudgetMs)
                    {
                        FinishCurrentEarly("per-metric work budget (" + _config.PerMetricWorkBudgetMs
                                           + " ms of measured work) reached", cancelled: false);
                    }
                    else if (_activeMs >= _config.TotalWorkBudgetMs)
                    {
                        FinishCurrentEarly("total work budget (" + _config.TotalWorkBudgetMs
                                           + " ms of measured work) reached", cancelled: false);
                    }

                    if (spentThisAdvance >= workBudgetMs)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                if (_current != null)
                {
                    _current.Row.Aborted = true;
                    _current.Row.AbortReason = e.GetType().Name + ": " + e.Message;
                    CompleteRow(_current.Row);
                    _completed.Add(_current.Row);
                    _current = null;
                }

                Error = e.GetType().Name + ": " + e.Message;
                Status = BenchJobStatus.Failed;
            }

            // Drain the backend's deferred-release queue.  xLua releases the native
            // references behind temporary tables/functions from a finalizer that only
            // ENQUEUES the work; LuaEnv.Tick() is what actually performs it, so a long
            // run must tick instead of relying on process shutdown.
            //
            // Tick() happens INSIDE the busy window: it is real work on the calling
            // thread, so charging it to "waiting between advances" would be wrong.
            try
            {
                if (_backend != null)
                {
                    _backend.Tick();
                }
            }
            catch (Exception tickError)
            {
                Error = Error ?? ("Tick failed: " + tickError.GetType().Name + ": " + tickError.Message);
            }

            advanceWatch.Stop();
            _advanceBusyMs += advanceWatch.Elapsed.TotalMilliseconds;

            if (Status != BenchJobStatus.Running)
            {
                _wall.Stop();
            }

            return Status != BenchJobStatus.Running;
        }

        private void CompleteCurrentTask()
        {
            _current.Finish?.Invoke(_current.Row);
            CompleteRow(_current.Row);
            _completed.Add(_current.Row);
            _current = null;
        }

        /// <summary>
        /// Ends the current task early, keeping whatever was measured.  Used for the
        /// per-metric/total work budget and for cancellation.
        /// </summary>
        private void FinishCurrentEarly(string reason, bool cancelled)
        {
            BenchRow row = _current.Row;
            row.Truncated = true;
            if (cancelled)
            {
                row.Cancelled = true;
            }

            row.Notes = AppendNote(row.Notes, reason);

            _current.Finish?.Invoke(_current.Row);
            CompleteRow(_current.Row);
            _completed.Add(_current.Row);
            _current = null;
        }

        /// <summary>Requests cancellation; the current step still finishes.</summary>
        public void Cancel()
        {
            _cancelRequested = true;
        }

        private bool MoveToNextTask()
        {
            _index++;
            if (_index >= _tasks.Count)
            {
                return false;
            }

            _current = _tasks[_index];
            PrepareRow(_current);

            if (_activeMs >= _config.TotalWorkBudgetMs)
            {
                _current.Row.Skipped = true;
                _current.Row.AbortReason = "total work budget (" + _config.TotalWorkBudgetMs
                                           + " ms of measured work) was already spent before this metric started";
                return true;
            }

            _current.Begin?.Invoke(_current.Row);
            return true;
        }

        private void PrepareRow(BenchTask task)
        {
            BenchRow row = task.Row;
            row.Metric = task.Metric;
            row.Unit = task.Unit;
            row.RequestedIterations = task.RequestedIterations;
            row.WarmupIterations = task.WarmupIterations;
            row.ExpectedChecksum = task.ExpectedExpression;
            row.PerformedIterations = 0;
            row.ActiveMs = 0;
            row.MeasuredMs = 0;
            row.WallMs = 0;
            row.Steps = 0;
            row.ChecksumValue = 0;
            row.ChecksumOk = false;
            row.Truncated = false;
            row.Aborted = false;
            row.Skipped = false;
            row.DeliveryVerified = false;
            row.ExpectedChecksumValue = long.MinValue;
            row.AbortReason = null;
            _rowWallStartTicks = Stopwatch.GetTimestamp();

            if (_config.CleanBaselineBeforeEachRow)
            {
                // Removes the previous row's garbage and drains finalizer queues
                // (NLua releases temporary table wrappers from a finalizer).
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            row.Gen0 = GC.CollectionCount(0);
            row.Gen1 = GC.CollectionCount(1);
            row.Gen2 = GC.CollectionCount(2);
            row.AllocatedBefore = GcProbe.TotalAllocated();
            row.ManagedBefore = GC.GetTotalMemory(false);
        }

        private void CompleteRow(BenchRow row)
        {
            row.WallMs = (Stopwatch.GetTimestamp() - _rowWallStartTicks) * 1000.0 / Stopwatch.Frequency;
            row.Gen0 = GC.CollectionCount(0) - row.Gen0;
            row.Gen1 = GC.CollectionCount(1) - row.Gen1;
            row.Gen2 = GC.CollectionCount(2) - row.Gen2;

            long allocatedAfter = GcProbe.TotalAllocated();
            if (row.AllocatedBefore >= 0 && allocatedAfter >= 0)
            {
                row.AllocatedBytes = allocatedAfter - row.AllocatedBefore;
            }

            row.ManagedDelta = GC.GetTotalMemory(false) - row.ManagedBefore;
            row.Checksum = row.ChecksumValue.ToString(CultureInfo.InvariantCulture);
            row.PerOpMicros = row.PerformedIterations > 0
                ? row.MeasuredMs * 1000.0 / row.PerformedIterations
                : double.NaN;

            Verify(row);
        }

        private static void Verify(BenchRow row)
        {
            // A skipped, aborted or empty row is never "verified": without this guard a
            // zero-iteration run would satisfy "expected == coefficient * 0".
            if (!row.IsVerifiable)
            {
                row.ChecksumOk = false;
                if (row.AbortReason == null)
                {
                    row.AbortReason = row.Skipped ? "skipped" : "no iterations completed";
                }

                return;
            }

            if (row.ExpectedChecksumValue != long.MinValue)
            {
                row.ChecksumOk = row.ChecksumValue == row.ExpectedChecksumValue && row.DeliveryVerified;
                if (!row.ChecksumOk && row.AbortReason == null)
                {
                    row.AbortReason = !row.DeliveryVerified
                        ? "bridge delivery counters did not match the expected crossings"
                        : "checksum " + row.ChecksumValue + " != expected " + row.ExpectedChecksumValue;
                }

                return;
            }

            long expected;
            if (!TryEvaluateExpected(row.ExpectedChecksum, row.PerformedIterations, out expected))
            {
                row.ChecksumOk = false;
                row.AbortReason = row.AbortReason ?? ("expected-checksum expression not evaluable: " + row.ExpectedChecksum);
                return;
            }

            row.ChecksumOk = row.ChecksumValue == expected;
            if (!row.ChecksumOk && row.AbortReason == null)
            {
                row.AbortReason = "checksum " + row.ChecksumValue + " != expected " + expected;
            }
        }

        private static bool TryEvaluateExpected(string expression, int iterations, out long expected)
        {
            expected = 0;

            if (string.IsNullOrEmpty(expression))
            {
                return false;
            }

            string trimmed = expression.Trim();

            if (trimmed == "iterations")
            {
                expected = iterations;
                return true;
            }

            string[] parts = trimmed.Split('*');
            if (parts.Length != 2)
            {
                return false;
            }

            long coefficient;
            if (!long.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out coefficient))
            {
                return false;
            }

            if (parts[1].Trim() != "iterations")
            {
                return false;
            }

            expected = coefficient * iterations;
            return true;
        }

        /// <summary>Freezes the collected rows into a result object.</summary>
        public BenchResult ToResult()
        {
            var result = new BenchResult
            {
                BackendId = BackendId,
                TotalWorkBudgetMs = _config.TotalWorkBudgetMs,
                Status = Status,
                ActiveMs = ActiveMs,
                WallMs = WallMs,
                AdvanceBusyMs = AdvanceBusyMs,
                OverheadMs = OverheadMs,
                IdleMs = IdleMs,
                SetupMs = SetupMs,
                AdvanceCount = _advanceCount,
                Error = Error,
                ExpectedRowCount = _tasks.Count,
            };

            result.Rows.AddRange(_completed);
            if (_current != null && _current.Row.PerformedIterations > 0 && !_completed.Contains(_current.Row))
            {
                result.Rows.Add(_current.Row);
            }

            return result;
        }

        /// <summary>One-line progress description for `luaprobe bench status`.</summary>
        public string Describe()
        {
            return "status=" + Status
                   + " backend=" + BackendId
                   + " rows=" + CompletedTaskCount + "/" + _tasks.Count
                   + " current=" + CurrentMetric
                   + " advances=" + _advanceCount
                   + " activeMs=" + ActiveMs.ToString("F1", CultureInfo.InvariantCulture)
                   + " wallMs=" + WallMs.ToString("F1", CultureInfo.InvariantCulture)
                   + " idleMs=" + IdleMs.ToString("F1", CultureInfo.InvariantCulture)
                   + (Error != null ? " error=" + Error : string.Empty);
        }
    }
}
