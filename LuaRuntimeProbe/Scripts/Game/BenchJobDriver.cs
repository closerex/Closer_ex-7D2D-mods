using System;
using System.Collections.Generic;
using LuaRuntimeProbe.Backends;
using LuaRuntimeProbe.Harness;
using UnityEngine;

namespace LuaRuntimeProbe.Game
{
    /// <summary>
    /// In-game driver for the shared benchmark kernel.
    ///
    /// A console command runs on the main thread and cannot yield, so `luaprobe bench`
    /// only REGISTERS the queue and returns; this component then performs the backend
    /// setup and advances the kernel by a few milliseconds of measured work per frame.
    /// All job state is static, so every completed backend's result stays readable after
    /// the ticking object is gone.
    ///
    /// WHAT IS AND IS NOT BOUNDED
    ///   * Each Update performs at most `PerAdvanceWorkBudgetMs` of measured work, so a
    ///     frame is not blocked for the length of the whole benchmark.
    ///   * A single step is NOT interruptible: the worst-case overrun is one chunk
    ///     (LoopChunk iterations, or CallbackChunk Lua-&gt;C# crossings).
    ///   * Backend setup (initialise + module load + bridge install) happens once per
    ///     backend on the frame that starts it, is NOT bounded by the per-frame budget,
    ///     and is reported as BenchResult.SetupMs rather than hidden.
    ///
    /// Footprint: exactly one hidden, don't-save helper GameObject while a queue is
    /// active, created on the main thread, touching no player/inventory/world or save
    /// state, destroyed when the queue drains.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class BenchJobDriver : MonoBehaviour
    {
        private const string HostName = "LuaRuntimeProbe.BenchDriver";

        private static GameObject _host;
        private static BenchConfig _config;
        private static BenchJob _job;
        private static BenchJob _recordedJob;
        private static readonly Queue<ILuaProbeBackend> Pending = new Queue<ILuaProbeBackend>();
        private static readonly List<BenchResult> Results = new List<BenchResult>();
        private static readonly List<string> Planned = new List<string>();
        private static readonly List<string> Notes = new List<string>();

        /// <summary>True while a queue is being driven.</summary>
        public static bool IsDriving
        {
            get { return _host != null; }
        }

        /// <summary>The job currently running, or the last job that ran.</summary>
        public static BenchJob CurrentJob
        {
            get { return _job; }
        }

        /// <summary>Results of every backend finished in the current queue.</summary>
        public static IList<BenchResult> CompletedResults
        {
            get { return Results; }
        }

        /// <summary>Backend ids still queued (an id leaves this list when its job starts).</summary>
        public static IList<string> QueuedBackends
        {
            get { return Planned; }
        }

        /// <summary>Operator-facing notes: cancellations, setup failures, dropped entries.</summary>
        public static IList<string> QueueNotes
        {
            get { return Notes; }
        }

        public static int CompletedCount
        {
            get { return Results.Count; }
        }

        public static int QueueLength { get; private set; }

        /// <summary>True when the current job has finished but its results have not been read.</summary>
        public static bool HasUnreadResults
        {
            get { return _job != null && _job.Status != BenchJobStatus.Running; }
        }

        /// <summary>
        /// Registers a queue.  This does NOT construct the first job: the kernel is built
        /// on the driver's first Update so the console command returns immediately and
        /// the (unbounded, reported) setup cost lands in a frame rather than in the
        /// command.
        /// </summary>
        public static bool StartQueue(IList<ILuaProbeBackend> backends, BenchConfig config, out string error)
        {
            error = null;

            if (_host != null)
            {
                error = "a benchmark queue is already running (" + DescribeQueue()
                        + "); use 'luaprobe bench status' or 'luaprobe bench cancel'";
                return false;
            }

            if (backends == null || backends.Count == 0)
            {
                error = "no backends selected";
                return false;
            }

            _config = config;
            _job = null;
            Pending.Clear();
            Results.Clear();
            Planned.Clear();
            Notes.Clear();

            foreach (ILuaProbeBackend backend in backends)
            {
                Pending.Enqueue(backend);
                Planned.Add(backend.Info.Id);
            }

            QueueLength = Planned.Count;
            EnsureHost();
            return true;
        }

        /// <summary>
        /// Requests cancellation.  The chunk in flight finishes, the partial row is
        /// completed and verified as a partial measurement, queued backends are dropped
        /// and the queue is reported as cancelled.
        /// </summary>
        public static void CancelQueue()
        {
            if (_host == null)
            {
                return;
            }

            if (Pending.Count > 0)
            {
                var ids = new List<string>();
                foreach (ILuaProbeBackend backend in Pending)
                {
                    ids.Add(backend.Info.Id);
                }

                Notes.Add("cancelled before running: " + string.Join(", ", ids.ToArray()));
            }

            Pending.Clear();
            Planned.Clear();

            if (_job != null)
            {
                _job.Cancel();
            }
        }

        /// <summary>Clears a finished queue's bookkeeping so a new queue can start.</summary>
        public static void ClearQueue()
        {
            if (_host != null)
            {
                return;
            }

            _job = null;
            Results.Clear();
            Planned.Clear();
            Notes.Clear();
            QueueLength = 0;
        }

        /// <summary>Advances the active job manually; used by `luaprobe bench step`.</summary>
        public static bool AdvanceByStep(double workBudgetMs, out string detail)
        {
            detail = null;

            if (_job == null)
            {
                if (Pending.Count > 0)
                {
                    StartNext();
                }
                else
                {
                    detail = "no benchmark job has been created";
                    return false;
                }
            }

            bool finished = _job.Advance(Math.Max(1, workBudgetMs));
            string steppedBackendId = _job.BackendId;
            detail = _job.Describe();

            if (_job.Status != BenchJobStatus.Running)
            {
                // Same completion path as the per-frame driver, so a manually stepped
                // queue cannot lose a backend's result (and cannot record it twice).
                CompleteCurrentJob();
                detail += " (recorded " + steppedBackendId + "); " + DescribeQueue();
            }

            return finished;
        }

        /// <summary>Human-readable queue state for `luaprobe bench status`.</summary>
        public static string DescribeQueue()
        {
            string current = _job != null ? _job.BackendId + "(" + _job.Status + ")" : "<none>";
            return "driving=" + IsDriving + " current=" + current
                   + " queued=" + Pending.Count + " completed=" + Results.Count + "/" + QueueLength;
        }

        private static void EnsureHost()
        {
            if (_host != null)
            {
                return;
            }

            _host = new GameObject(HostName);
            _host.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<BenchJobDriver>();
        }

        private static void TeardownHost()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }
        }

        private static void StartNext()
        {
            if (Pending.Count == 0)
            {
                TeardownHost();
                return;
            }

            ILuaProbeBackend backend = Pending.Dequeue();
            Planned.Remove(backend.Info.Id);

            string createError;
            BenchJob job = BenchJob.Create(backend, _config, out createError);

            if (job == null)
            {
                Results.Add(new BenchResult
                {
                    BackendId = backend.Info.Id,
                    Status = BenchJobStatus.Failed,
                    Error = createError,
                    TotalWorkBudgetMs = _config != null ? _config.TotalWorkBudgetMs : 0,
                });

                Notes.Add(backend.Info.Id + " could not be set up: " + createError);
                StartNext();
                return;
            }

            _job = job;
        }

        /// <summary>Records the finished job exactly once and moves the queue on.</summary>
        private static void CompleteCurrentJob()
        {
            if (_job == null)
            {
                return;
            }

            // Idempotence: a manual `bench step` issued after the queue already finished
            // must not record the same backend's result a second time.  The ticker and
            // AdvanceByStep both route through here, so the guard lives here.
            if (ReferenceEquals(_recordedJob, _job))
            {
                TeardownHost();
                return;
            }

            _recordedJob = _job;
            string finishedBackendId = _job.BackendId;
            Results.Add(_job.ToResult());

            if (Pending.Count > 0 && _job.Status == BenchJobStatus.Completed)
            {
                // Capture the finished backend's identity BEFORE moving on, so the
                // operator-facing label cannot name the next backend by mistake.
                Notes.Add("completed " + finishedBackendId + " (" + _job.Status + ")");
                _job = null;
                StartNext();
                return;
            }

            Notes.Add("completed " + finishedBackendId + " (" + _job.Status + ")");
            _job = null;
            TeardownHost();
        }

        private void Update()
        {
            if (_job == null)
            {
                if (Pending.Count > 0)
                {
                    // Backend setup happens here (first frame after `bench start`), not
                    // inside the console command.
                    StartNext();
                    return;
                }

                TeardownHost();
                return;
            }

            double budget = _config != null ? Math.Max(1, _config.PerAdvanceWorkBudgetMs) : 4;
            _job.Advance(budget);

            if (_job.Status != BenchJobStatus.Running)
            {
                CompleteCurrentJob();
            }
        }
    }
}
