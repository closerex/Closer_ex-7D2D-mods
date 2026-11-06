using LuaRuntimeProbe.Contracts;

namespace LuaRuntimeProbe.Harness
{
    /// <summary>
    /// Everything the Lua backends and the harness need from their environment.
    ///
    /// Splitting this out is what makes the runtime comparison testable OUTSIDE
    /// the game: the identical backend + suite + benchmark code can be driven
    /// from a plain console process with an offline host, so only the game
    /// integration (console command, player context, logging) is untested until
    /// the in-game run.
    /// </summary>
    public interface IProbeHost
    {
        /// <summary>Absolute path of the folder containing the probe .lua files, or null.</summary>
        string LuaDirectory { get; }

        /// <summary>Absolute path of the folder containing native lua54.dll, or null.</summary>
        string NativeDirectory { get; }

        /// <summary>Reads a mod-relative text file, or returns null and sets error.</summary>
        string ReadModFile(string relativePath, out string error);

        /// <summary>Captures the read-only player/world snapshot.</summary>
        PlayerSnapshot CapturePlayer();

        void LogInfo(string message);

        void LogWarning(string message);
    }

    /// <summary>Process-wide host used by the backends and the harness.</summary>
    public static class ProbeHost
    {
        private static IProbeHost _current;

        public static IProbeHost Current
        {
            get { return _current ?? NullProbeHost.Instance; }
            set { _current = value; }
        }

        public static bool Installed
        {
            get { return _current != null; }
        }
    }

    /// <summary>
    /// Fallback host used when nothing installed one.  It fails explicitly
    /// instead of pretending to work: no directories, no files, no world.
    /// </summary>
    public sealed class NullProbeHost : IProbeHost
    {
        public static readonly NullProbeHost Instance = new();

        public string LuaDirectory
        {
            get { return null; }
        }

        public string NativeDirectory
        {
            get { return null; }
        }

        public string ReadModFile(string relativePath, out string error)
        {
            error = "no probe host installed (ProbeHost.Current is null); cannot read " + relativePath;
            return null;
        }

        public PlayerSnapshot CapturePlayer()
        {
            return PlayerSnapshot.Unavailable("world_unavailable: no probe host installed");
        }

        public void LogInfo(string message)
        {
        }

        public void LogWarning(string message)
        {
        }
    }
}
