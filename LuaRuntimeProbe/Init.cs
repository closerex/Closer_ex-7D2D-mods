using UnityEngine.Scripting;

namespace LuaRuntimeProbe
{
    /// <summary>
    /// Mod entry point.  Deliberately thin: the console command class is
    /// discovered and instantiated by the game itself
    /// (SdtdConsole.RegisterCommands -> ReflectionHelpers.FindTypesImplementingBase),
    /// so this class only installs the probe host and announces itself.
    /// </summary>
    [Preserve]
    public class LuaProbeInit : IModApi
    {
        public static bool Initialized;

        /// <summary>Folder the mod was loaded from (== folder containing this DLL).</summary>
        public static string ModPath;

        public void InitMod(Mod _modInstance)
        {
            Game.GameProbeHost.Install();

            if (Initialized)
            {
                Log.Out("[LuaRuntimeProbe] InitMod called again - ignoring (already initialised)");
                return;
            }

            Initialized = true;
            ModPath = _modInstance != null ? _modInstance.Path : null;

            Log.Out("[LuaRuntimeProbe] InitMod mod={0} path={1} resolved={2}",
                _modInstance != null ? _modInstance.Name : "<null>",
                ModPath ?? "<null>",
                Game.ModPaths.ResolveModDirectory() ?? "<null>");
            Log.Out("[LuaRuntimeProbe] Console commands: luaprobe status|list|ctx|test|run|bench|reset");
        }
    }
}
