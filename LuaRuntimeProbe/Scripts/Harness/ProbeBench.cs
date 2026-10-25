using System;
using LuaRuntimeProbe.Backends;

namespace LuaRuntimeProbe.Harness
{
    /// <summary>
    /// SYNCHRONOUS driver for the shared <see cref="BenchJob"/> kernel.
    ///
    /// Used by the offline harness and by the explicit `luaprobe bench sync` form.
    /// The in-game default is the cross-frame driver (see
    /// LuaRuntimeProbe.Game.BenchJobDriver), which advances the very same kernel once
    /// per frame - so both paths share the task list, the accumulators and the
    /// verification, and differ only in who calls Advance() and how often.
    ///
    /// This driver blocks the calling thread for the duration of the run.  That is
    /// acceptable offline and for an explicit opt-in in game, but it is NOT what the
    /// in-game default does.
    /// </summary>
    public static class ProbeBench
    {
        /// <summary>Hard stop for the synchronous loop, so a bug cannot spin forever.</summary>
        public const int MaxAdvances = 2000000;

        public static BenchResult Run(ILuaProbeBackend backend, BenchConfig config)
        {
            string error;
            BenchJob job = BenchJob.Create(backend, config, out error);

            if (job == null)
            {
                return new BenchResult
                {
                    BackendId = backend != null ? backend.Info.Id : "<none>",
                    Status = BenchJobStatus.Failed,
                    Error = error,
                };
            }

            return Drive(job, config);
        }

        /// <summary>Advances an existing job to completion on the calling thread.</summary>
        public static BenchResult Drive(BenchJob job, BenchConfig config)
        {
            // The slice is a SMALL amount of measured work per Advance(); the per-metric
            // budget is what caps an individual metric.  Using the per-metric budget as
            // the slice too would truncate every metric after a single chunk.
            int slice = Math.Max(1, Math.Min(50, config.PerMetricWorkBudgetMs));
            int guard = 0;

            while (!job.Advance(slice))
            {
                if (++guard > MaxAdvances)
                {
                    job.Cancel();
                    job.Advance(0);
                    break;
                }
            }

            return job.ToResult();
        }
    }
}
