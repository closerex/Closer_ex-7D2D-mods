using System;
using System.IO;
using LuaRuntimeProbe.Contracts;
using LuaRuntimeProbe.Harness;

namespace LuaRuntimeProbe.OfflineProbe
{
    /// <summary>
    /// Host implementation for the offline harness.
    ///
    /// It mirrors the mod's folder layout exactly (Lua\ next to the assembly,
    /// native\lua54.dll below it) so the same backend code paths are exercised.
    /// The player context intentionally reports "unavailable": there is no game
    /// world here, which also proves the explicit-unavailable branch works.
    /// </summary>
    public sealed class OfflineProbeHost : IProbeHost
    {
        private readonly string _modRoot;

        public OfflineProbeHost(string modRoot)
        {
            _modRoot = modRoot;
        }

        public string LuaDirectory
        {
            get { return Path.Combine(_modRoot, "Lua"); }
        }

        public string NativeDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "native"); }
        }

        public string ReadModFile(string relativePath, out string error)
        {
            error = null;

            try
            {
                string full = Path.Combine(_modRoot, relativePath);
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

        public PlayerSnapshot CapturePlayer()
        {
            // LUAPROBE_FAKE_WORLD=1 exercises the "world IS available" branch of the
            // read-only context checks offline.  Without it that branch is unreachable
            // outside the game - and it is the branch that matters in game, so leaving
            // it untested offline would hide exactly the kind of defect where a check
            // placed after the branch's early return silently disappears.
            if (Environment.GetEnvironmentVariable("LUAPROBE_FAKE_WORLD") == "1")
            {
                return new PlayerSnapshot
                {
                    Available = true,
                    Name = "offline-fake-player",
                    DisplayName = "Offline Fake Player",
                    Health = 87,
                    MaxHealth = 100,
                    Stamina = 55.5f,
                    GameStage = 12,
                    IsDead = false,
                    IsAdmin = false,
                    X = 1.5f,
                    Y = 2.5f,
                    Z = 3.5f,
                    WorldGuid = "offline-fake-guid",
                    WorldDay = 3,
                    WorldHour = 9,
                    CapturedOnFrame = 4242,
                };
            }

            return PlayerSnapshot.Unavailable(
                "world_unavailable: offline probe host has no game world (expected outside 7 Days to Die)");
        }

        public void LogInfo(string message)
        {
            Console.WriteLine("[info] " + message);
        }

        public void LogWarning(string message)
        {
            Console.WriteLine("[warn] " + message);
        }
    }
}
