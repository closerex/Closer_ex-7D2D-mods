using System;
using System.Collections.Generic;
using LuaRuntimeProbe.Harness;

namespace LuaRuntimeProbe.Backends
{
    /// <summary>
    /// Creates and caches the backends.  The registry is process-wide because
    /// the console command object is instantiated by the game itself and a
    /// fresh Lua state per command invocation would make the "cold init vs warm
    /// call" split impossible to observe.
    /// </summary>
    public static class LuaBackendRegistry
    {
        private static readonly object SyncRoot = new object();

        private static readonly List<ILuaProbeBackend> Backends = new List<ILuaProbeBackend>();
        private static bool _created;

        public static IList<ILuaProbeBackend> All
        {
            get
            {
                EnsureCreated();
                return Backends;
            }
        }

        private static void EnsureCreated()
        {
            if (_created)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_created)
                {
                    return;
                }

                Backends.Add(new MoonSharpBackend());
                Backends.Add(new NLuaBackend());
                Backends.Add(new XLuaBackend());
                _created = true;
            }
        }

        /// <summary>Returns the backend with the given id, or null.</summary>
        public static ILuaProbeBackend Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (ILuaProbeBackend backend in All)
            {
                if (string.Equals(backend.Info.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return backend;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a brand new, uninitialised backend instance of the same kind.
        /// Used to measure cold initialisation without disturbing the cached one.
        /// </summary>
        public static ILuaProbeBackend CreateNew(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (string.Equals(id, "moonsharp", StringComparison.OrdinalIgnoreCase))
            {
                return new MoonSharpBackend();
            }

            if (string.Equals(id, "nlua", StringComparison.OrdinalIgnoreCase))
            {
                return new NLuaBackend();
            }

            if (string.Equals(id, "xlua", StringComparison.OrdinalIgnoreCase))
            {
                return new XLuaBackend();
            }

            return null;
        }

        /// <summary>"moonsharp,nlua,xlua" - used in usage messages.</summary>
        public static string IdList()
        {
            var ids = new List<string>();
            foreach (ILuaProbeBackend backend in All)
            {
                ids.Add(backend.Info.Id);
            }

            return string.Join(",", ids.ToArray());
        }

        /// <summary>
        /// Shuts every backend down and clears retryable failures, so the next
        /// command really does re-initialise (a permanent failure such as a
        /// missing native library cannot be cleared).
        /// </summary>
        public static void ShutdownAll()
        {
            lock (SyncRoot)
            {
                foreach (ILuaProbeBackend backend in Backends)
                {
                    try
                    {
                        backend.Shutdown();
                        backend.ClearRetryableFailure();
                    }
                    catch (Exception e)
                    {
                        ProbeHost.Current.LogWarning("[LuaRuntimeProbe] shutdown of " + backend.Info.Id + " failed: " + e.Message);
                    }
                }
            }
        }

        /// <summary>Drops cached player snapshots on every backend.</summary>
        public static void InvalidatePlayerSnapshots()
        {
            foreach (ILuaProbeBackend backend in All)
            {
                try
                {
                    backend.InvalidatePlayerSnapshot();
                }
                catch (Exception e)
                {
                    ProbeHost.Current.LogWarning("[LuaRuntimeProbe] snapshot invalidation for "
                                                 + backend.Info.Id + " failed: " + e.Message);
                }
            }
        }
    }
}
