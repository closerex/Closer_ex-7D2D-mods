using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LuaRuntimeProbe.Backends;
using LuaRuntimeProbe.Harness;

namespace LuaRuntimeProbe.OfflineProbe
{
    /// <summary>
    /// Entry point of the offline harness.
    ///
    /// Exit code 0 = every assertion passed.  Exit code 1 = at least one
    /// assertion failed, or a backend that should have been runnable was not.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            bool runBench = false;
            bool noCaps = false;
            bool selfCheck = false;
            int benchBudgetMs = 3000;
            string filter = null;
            string modRootOverride = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--selfcheck":
                        // Mechanical regression checks for paths that are hard to reach in
                        // game: cancellation at each boundary, and the "no work" guards.
                        selfCheck = true;
                        break;
                    case "--nocaps":
                        // Mirrors `luaprobe test <backend>` without --caps, so the
                        // assertion count of both modes can be compared exactly.
                        noCaps = true;
                        break;
                    case "--bench":
                        runBench = true;
                        // --bench may be followed by an optional budget in ms.
                        if (i + 1 < args.Length
                            && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int inlineBudget))
                        {
                            benchBudgetMs = inlineBudget;
                            i++;
                        }
                        break;
                    case "--budget":
                        if (i + 1 < args.Length)
                        {
                            int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out benchBudgetMs);
                        }
                        break;
                    case "--filter":
                        if (i + 1 < args.Length)
                        {
                            filter = args[++i];
                        }
                        break;
                    case "--modroot":
                        if (i + 1 < args.Length)
                        {
                            modRootOverride = args[++i];
                        }
                        break;
                }
            }

            string modRoot = modRootOverride ?? LocateModRoot();
            if (modRoot == null)
            {
                Console.Error.WriteLine("FATAL: could not locate the LuaRuntimeProbe mod folder.");
                Console.Error.WriteLine("       Pass --modroot <path to ModsRepo\\LuaRuntimeProbe>.");
                return 2;
            }

            Console.WriteLine("LuaRuntimeProbe offline harness");
            Console.WriteLine("  modRoot      = " + modRoot);
            Console.WriteLine("  lauDir       = " + Path.Combine(modRoot, "Lua"));
            Console.WriteLine("  nativeDir    = " + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "native"));
            Console.WriteLine("  clr          = " + Environment.Version + " (" + (IntPtr.Size * 8) + "-bit)");
            Console.WriteLine("  mono         = " + (Type.GetType("Mono.Runtime") != null ? "yes" : "no"));
            Console.WriteLine("  gcAllocProbe = " + (GcProbe.Available ? "available" : "unavailable"));
            Console.WriteLine("  withXLua     = "
#if LUA_PROBE_XLUA
                              + "yes (the xLua backend is compiled in and must pass)"
#else
                              + "no (the xLua backend is a documented stub)"
#endif
            );
            Console.WriteLine("  gameRunning  = " + GameProcessRunning()
                              + "   <-- if true, this machine is also running the game, so timings are contended");
            Console.WriteLine();

            ProbeHost.Current = new OfflineProbeHost(modRoot);

            if (selfCheck)
            {
                return RunSelfCheck();
            }

            int failures = 0;
            var ranBackends = new List<string>();
            var selectedIds = new List<string>();

            foreach (ILuaProbeBackend backend in LuaBackendRegistry.All)
            {
                if (!string.IsNullOrEmpty(filter)
                    && !string.Equals(backend.Info.Id, filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                selectedIds.Add(backend.Info.Id);

                Console.WriteLine("================ functional suite: " + backend.Info.Id + " ================");
                Console.WriteLine("  embed        : " + backend.Info.EmbedVersion);
                Console.WriteLine("  lua language : declared=" + backend.Info.DeclaredLuaLanguageVersion
                                  + " measured=" + (backend.Info.MeasuredLuaVersion ?? "<not initialised>"));
                Console.WriteLine("  runtime      : " + backend.Info.RuntimeKind);
                Console.WriteLine("  native dep   : " + backend.Info.NativeDependency);
                Console.WriteLine("  available    : " + backend.IsAvailable
                                  + (backend.IsAvailable ? string.Empty : " (" + backend.UnavailableReason + ")"));

                ProbeSuiteResult result = ProbeSuite.Run(backend, !noCaps);

                foreach (ProbeCheck check in result.Checks)
                {
                    Console.WriteLine("  " + check.Render());
                }

                result.Tally();
                failures += result.FailedCount;

                Console.WriteLine("  --> pass=" + result.PassedCount + " fail=" + result.FailedCount
                                  + " info=" + result.InfoCount
                                  + (result.Ran ? string.Empty : " (suite did not run)"));

                if (result.Ran)
                {
                    ranBackends.Add(backend.Info.Id);
                }

                Console.WriteLine();
            }

            foreach (ILuaProbeBackend backend in LuaBackendRegistry.All)
            {
                // Validation must use the SAME selection as the run, otherwise a
                // single-backend --filter run is reported as a failure because the
                // backends it never asked for did not run.
                if (!selectedIds.Contains(backend.Info.Id))
                {
                    continue;
                }

                if (backend.Info.Id == "xlua")
                {
#if !LUA_PROBE_XLUA
                    // In a default build the xLua backend is a documented stub, and the
                    // suite already prints its reason.  With -p:WithXLua=true it becomes a
                    // full peer and must be runnable, so it is not skipped below.
                    continue;
#endif
                }

                if (!backend.IsAvailable)
                {
                    Console.WriteLine("NOTE: backend '" + backend.Info.Id
                                      + "' is unavailable: " + backend.UnavailableReason);
                    failures++;
                }
                else if (!ranBackends.Contains(backend.Info.Id))
                {
                    Console.WriteLine("NOTE: backend '" + backend.Info.Id
                                      + "' is available but its suite did not run");
                    failures++;
                }
            }

            if (runBench)
            {
                BenchConfig config = BenchConfig.Default();
                config.TotalWorkBudgetMs = benchBudgetMs;
                config.PerMetricWorkBudgetMs = Math.Max(50, benchBudgetMs / 4);

                foreach (string id in ranBackends)
                {
                    ILuaProbeBackend backend = LuaBackendRegistry.Find(id);
                    Console.WriteLine("================ benchmark: " + id + " ================");
                    Console.WriteLine("  budget total=" + config.TotalWorkBudgetMs + "ms perMetric=" + config.PerMetricWorkBudgetMs + "ms"
                                      + " hot=" + config.HotIterations + " dto=" + config.DtoIterations
                                      + " callback=" + config.CallbackIterations + " compile=" + config.CompileIterations
                                      + " coldInit=" + config.ColdInitIterations);

                    BenchResult bench = ProbeBench.Run(backend, config);
                    if (!string.IsNullOrEmpty(bench.Error))
                    {
                        Console.WriteLine("  UNRUNNABLE: " + bench.Error);
                        failures++;
                        continue;
                    }

                    Console.WriteLine("  active_work=" + F(bench.ActiveMs) + "ms  wall=" + F(bench.WallMs)
                                      + "ms  idle=" + F(bench.IdleMs) + "ms  overhead=" + F(bench.OverheadMs)
                                      + "ms  setup=" + F(bench.SetupMs) + "ms  status=" + bench.Status
                                      + " measured lua=" + (backend.Info.MeasuredLuaVersion ?? "?"));
                    Console.WriteLine("  " + Pad("metric", 31) + Pad("iters(req/done/warm)", 24) + Pad("active_ms", 10)
                                      + Pad("wall_ms", 10) + Pad("us/op", 10) + Pad("gen0/1/2", 11)
                                      + Pad("allocated", 12) + Pad("managed", 12) + Pad("checksum", 10)
                                      + Pad("ok", 4) + "flag");

                    foreach (BenchRow row in bench.Rows)
                    {
                        Console.WriteLine("  " + Pad(row.Metric, 31)
                                          + Pad(row.RequestedIterations + "/" + row.PerformedIterations + "/" + row.WarmupIterations, 24)
                                          + Pad(F(row.ActiveMs), 10)
                                          + Pad(F(row.WallMs), 10)
                                          + Pad(F(row.PerOpMicros), 10)
                                          + Pad(row.Gen0 + "/" + row.Gen1 + "/" + row.Gen2, 11)
                                          + Pad(row.AllocatedBytes < 0 ? "n/a" : row.AllocatedBytes.ToString(CultureInfo.InvariantCulture), 12)
                                          + Pad(row.ManagedDelta.ToString(CultureInfo.InvariantCulture), 12)
                                          + Pad(row.Checksum, 10)
                                          + Pad(row.ChecksumOk ? "Y" : "N", 4)
                                          + FlagOf(row));

                        if (row.Skipped)
                        {
                            // A skip is a declared budget outcome, not a defect: the kernel
                            // refused to measure the metric rather than reporting a token run.
                            Console.WriteLine("      SKIPPED (not a failure): " + row.AbortReason);
                        }
                        else if (!row.IsVerifiable || !row.ChecksumOk)
                        {
                            failures++;
                            Console.WriteLine("      NOT VERIFIED: " + (row.AbortReason ?? "checksum mismatch"));
                            Console.WriteLine("      EXPECTED CHECKSUM: " + row.ExpectedChecksum);
                        }

                        if (!string.IsNullOrEmpty(row.Notes))
                        {
                            Console.WriteLine("      note: " + row.Notes);
                        }
                    }

                    Console.WriteLine("  flag census: " + bench.FlagCensus());

                    if (!bench.AllRowsPresent)
                    {
                        failures++;
                        Console.WriteLine("      ROW COUNT MISMATCH: the kernel produced " + bench.Rows.Count
                                          + " row(s) for " + bench.ExpectedRowCount + " requested metric(s)");
                    }

                    Console.WriteLine();
                }
            }

            Console.WriteLine(failures == 0
                ? "RESULT: all assertions passed"
                : "RESULT: " + failures + " failed assertion(s)");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>
        /// Regression checks for the benchmark kernel's edge paths.  These exist because a
        /// reviewer reproduced a crash for cancellation at a boundary where no row is in
        /// flight; the check keeps that from silently returning.
        /// </summary>
        private static int RunSelfCheck()
        {
            int failures = 0;
            Console.WriteLine("=== kernel self-check ===");

            ILuaProbeBackend backend = LuaBackendRegistry.Find("moonsharp");
            if (backend == null)
            {
                Console.WriteLine("  FAIL: moonsharp backend not registered");
                return 1;
            }

            // A. cancel before the first advance: there is no partial row to finish.
            string errorA;
            BenchConfig configA = BenchConfig.Default();
            BenchJob jobA = BenchJob.Create(backend, configA, out errorA);
            if (jobA == null)
            {
                Console.WriteLine("  FAIL cancel-before-first-advance: Create failed: " + errorA);
                failures++;
            }
            else
            {
                jobA.Cancel();
                jobA.Advance(1);
                bool okA = jobA.Status == BenchJobStatus.Cancelled && jobA.Error == null;
                Console.WriteLine("  " + (okA ? "PASS" : "FAIL") + " cancel-before-first-advance: status="
                                  + jobA.Status + " rows=" + jobA.CompletedRows.Count
                                  + " error=" + (jobA.Error ?? "<none>"));
                if (!okA)
                {
                    failures++;
                }
            }

            // B. cancel between two metrics: at least one row finished, none in flight.
            string errorB;
            BenchConfig configB = BenchConfig.Default();
            configB.HotIterations = 2000;
            configB.DtoIterations = 200;
            configB.CallbackIterations = 200;
            BenchJob jobB = BenchJob.Create(backend, configB, out errorB);
            if (jobB == null)
            {
                Console.WriteLine("  FAIL cancel-between-metrics: Create failed: " + errorB);
                failures++;
            }
            else
            {
                int guard = 0;
                while (jobB.CompletedRows.Count == 0 && jobB.Status == BenchJobStatus.Running && guard++ < 100000)
                {
                    jobB.Advance(50);
                }

                jobB.Cancel();
                jobB.Advance(1);
                bool okB = jobB.Status == BenchJobStatus.Cancelled && jobB.Error == null && jobB.CompletedRows.Count >= 1;
                Console.WriteLine("  " + (okB ? "PASS" : "FAIL") + " cancel-between-metrics: status="
                                  + jobB.Status + " rows=" + jobB.CompletedRows.Count
                                  + " error=" + (jobB.Error ?? "<none>"));
                if (!okB)
                {
                    failures++;
                }
            }

            // C. cancel while a row is in flight: the partial row must be completed,
            //    flagged C, and still verified against the iterations it did perform.
            string errorC;
            BenchConfig configC = BenchConfig.Default();
            configC.HotIterations = 5000000;
            configC.DtoIterations = 5000000;
            configC.BridgeDirectIterations = 5000000;
            configC.CompileIterations = 5000000;
            configC.ColdInitIterations = 5000000;
            configC.CallbackIterations = 5000000;
            configC.PerMetricWorkBudgetMs = 0;      // disable the per-metric cap
            configC.TotalWorkBudgetMs = int.MaxValue;
            configC.CleanBaselineBeforeEachRow = false;
            BenchJob jobC = BenchJob.Create(backend, configC, out errorC);
            if (jobC == null)
            {
                Console.WriteLine("  FAIL cancel-mid-metric: Create failed: " + errorC);
                failures++;
            }
            else
            {
                jobC.Advance(5);   // one bounded step into the first metric
                jobC.Cancel();
                jobC.Advance(1);

                BenchRow partial = jobC.CompletedRows.Count > 0
                    ? jobC.CompletedRows[jobC.CompletedRows.Count - 1]
                    : null;

                bool okC = jobC.Status == BenchJobStatus.Cancelled
                           && jobC.Error == null
                           && partial != null
                           && partial.Cancelled
                           && partial.Flag == "C"
                           && partial.IsVerifiable
                           && partial.ChecksumOk
                           && partial.PerformedIterations > 0;

                Console.WriteLine("  " + (okC ? "PASS" : "FAIL") + " cancel-mid-metric: status=" + jobC.Status
                                  + " row=" + (partial == null ? "<none>" : partial.Metric)
                                  + " flag=" + (partial == null ? "-" : partial.Flag)
                                  + " performed=" + (partial == null ? 0 : partial.PerformedIterations)
                                  + " checksumOk=" + (partial != null && partial.ChecksumOk)
                                  + " error=" + (jobC.Error ?? "<none>"));
                if (!okC)
                {
                    failures++;
                }
            }

            // D. a row can never be "verified" without doing work.
            var emptyRow = new BenchRow { Metric = "synthetic", ExpectedChecksum = "iterations" };
            bool okD = !emptyRow.IsVerifiable && emptyRow.Flag == "-";
            Console.WriteLine("  " + (okD ? "PASS" : "FAIL")
                              + " zero-iteration row is never verifiable: IsVerifiable=" + emptyRow.IsVerifiable);
            if (!okD)
            {
                failures++;
            }

            Console.WriteLine(failures == 0
                ? "SELFCHECK RESULT: all kernel edge checks passed"
                : "SELFCHECK RESULT: " + failures + " failed");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>
        /// Reports whether 7 Days to Die is running on this machine.  Timings taken
        /// while it is are contended, so the run must say so rather than present them
        /// as clean measurements.
        /// </summary>
        private static string GameProcessRunning()
        {
            try
            {
                System.Diagnostics.Process[] processes =
                    System.Diagnostics.Process.GetProcessesByName("7DaysToDie");
                return processes != null && processes.Length > 0 ? "true" : "false";
            }
            catch (Exception e)
            {
                return "<unavailable: " + e.GetType().Name + ">";
            }
        }

        private static string LocateModRoot()
        {
            string env = Environment.GetEnvironmentVariable("LUAPROBE_MODROOT");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "Lua")))
            {
                return env;
            }

            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                string candidate = Path.Combine(dir.FullName, "LuaRuntimeProbe", "Lua", "probe_core.lua");
                if (File.Exists(candidate))
                {
                    return Path.Combine(dir.FullName, "LuaRuntimeProbe");
                }

                string sibling = Path.Combine(dir.FullName, "Lua", "probe_core.lua");
                if (File.Exists(sibling))
                {
                    return dir.FullName;
                }

                // Running from LuaRuntimeProbe\tests\...\bin -> walk up and across.
                string parentSibling = dir.Parent != null
                    ? Path.Combine(dir.Parent.FullName, "LuaRuntimeProbe", "Lua", "probe_core.lua")
                    : null;
                if (parentSibling != null && File.Exists(parentSibling))
                {
                    return Path.Combine(dir.Parent.FullName, "LuaRuntimeProbe");
                }

                dir = dir.Parent;
            }

            return null;
        }

        private static string FlagOf(BenchRow row)
        {
            return row.Flag;
        }

        private static string Pad(string text, int width)
        {
            string value = text ?? string.Empty;
            return value.Length >= width ? value + " " : value + new string(' ', width - value.Length);
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
