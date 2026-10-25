using System;
using System.IO;
using System.Reflection;
using LuaRuntimeProbe.Contracts;
using LuaRuntimeProbe.Harness;

namespace LuaRuntimeProbe.Game
{
    /// <summary>
    /// Game-side probe host: resolves the mod folder and reads the read-only
    /// player/world snapshot.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class GameProbeHost : IProbeHost
    {
        public static readonly GameProbeHost Instance = new GameProbeHost();

        private static bool _installed;

        /// <summary>Idempotently makes this the active host. Called from mod init and the console command.</summary>
        public static void Install()
        {
            if (_installed)
            {
                return;
            }

            ProbeHost.Current = Instance;
            _installed = true;
            VerifyModRootIsLoadable();
        }

        /// <summary>
        /// Checks that every *.dll in the mod ROOT is a managed assembly.
        ///
        /// Mod.LoadAssemblies() calls Assembly.LoadFrom() on every *.dll directly
        /// inside the mod folder; a native library there throws
        /// BadImageFormatException and the game aborts the whole mod with
        /// EModLoadState.FailedLoadingAssembly.  Anything non-managed found at the
        /// root is reported loudly, because the symptom otherwise looks like
        /// "the mod silently does not exist".
        /// </summary>
        public static void VerifyModRootIsLoadable()
        {
            string root = ModPaths.ResolveModDirectory();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                Log.Warning("[LuaRuntimeProbe] mod root could not be resolved; skipping the loadability self-check");
                return;
            }

            int checkedCount = 0;
            foreach (string file in Directory.GetFiles(root, "*.dll"))
            {
                checkedCount++;
                try
                {
                    System.Reflection.AssemblyName.GetAssemblyName(file);
                }
                catch (Exception e)
                {
                    Log.Error("[LuaRuntimeProbe] LOADABILITY HAZARD: " + file
                              + " is not a managed assembly (" + e.GetType().Name + "). "
                              + "7DTD loads every *.dll in the mod root; this file would make the mod fail to load. "
                              + "Move it to a subfolder such as native\\ and side-load it explicitly.");
                }
            }

            Log.Out("[LuaRuntimeProbe] loadability self-check: " + checkedCount
                    + " managed DLL(s) in the mod root, native payload expected under " + ModPaths.NativeDirectory);
        }

        public string LuaDirectory
        {
            get { return ModPaths.LuaDirectory; }
        }

        public string NativeDirectory
        {
            get { return ModPaths.NativeDirectory; }
        }

        public string ReadModFile(string relativePath, out string error)
        {
            return ModPaths.TryReadModFile(relativePath, out error);
        }

        public PlayerSnapshot CapturePlayer()
        {
            return GameContextProvider.Capture();
        }

        public void LogInfo(string message)
        {
            Log.Out(message);
        }

        public void LogWarning(string message)
        {
            Log.Warning(message);
        }
    }

    /// <summary>
    /// Resolves the mod folder independently of InitMod ordering.
    ///
    /// SdtdConsole.RegisterCommands() runs during the SdtdConsole singleton's
    /// Awake, which may happen before or after IModApi.InitMod.  Deriving the
    /// folder from this assembly's own Location is therefore the primary source
    /// of truth; Mod.Path is only used as a fallback.
    /// </summary>
    public static class ModPaths
    {
        private static string _cached;

        public static string ResolveModDirectory()
        {
            if (!string.IsNullOrEmpty(_cached))
            {
                return _cached;
            }

            try
            {
                string location = typeof(GameProbeHost).Assembly.Location;
                if (!string.IsNullOrEmpty(location))
                {
                    _cached = Path.GetDirectoryName(location);
                    return _cached;
                }
            }
            catch (Exception e)
            {
                Log.Warning("[LuaRuntimeProbe] Could not derive mod directory from assembly location: " + e.Message);
            }

            if (!string.IsNullOrEmpty(LuaProbeInit.ModPath))
            {
                _cached = LuaProbeInit.ModPath;
            }

            return _cached;
        }

        public static string LuaDirectory
        {
            get { return Path.Combine(ResolveModDirectory() ?? ".", "Lua"); }
        }

        public static string NativeDirectory
        {
            get { return Path.Combine(ResolveModDirectory() ?? ".", "native"); }
        }

        /// <summary>Reads a mod-relative text file, or returns null with a reason.</summary>
        public static string TryReadModFile(string relativePath, out string error)
        {
            error = null;
            try
            {
                string root = ResolveModDirectory();
                if (string.IsNullOrEmpty(root))
                {
                    error = "mod directory could not be resolved";
                    return null;
                }

                string full = Path.Combine(root, relativePath);
                if (!File.Exists(full))
                {
                    error = "file not found: " + full;
                    return null;
                }

                return File.ReadAllText(full);
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }
    }
}
