using System;
using System.Collections.Generic;
using System.Globalization;
using LuaRuntimeProbe.Contracts;

namespace LuaRuntimeProbe.Contracts
{
    /// <summary>
    /// The C# object exposed to Lua as the global `probe`.
    ///
    /// It is intentionally tiny and side-effect free with respect to the game
    /// world: it can only read a previously captured player snapshot and record
    /// counters.  It never writes player, inventory, world or save state.
    /// </summary>
    public sealed class ProbeBridge
    {
        private readonly Func<PlayerSnapshot> _capture;

        private PlayerSnapshot _snapshot;

        public ProbeBridge(Func<PlayerSnapshot> capture)
        {
            _capture = capture ?? (() => PlayerSnapshot.Unavailable("no capture delegate configured"));
        }

        /// <summary>Number of Lua -> C# callback invocations observed.</summary>
        public int EmitCount { get; private set; }

        /// <summary>Sum of the ids passed to Emit - lets the host verify every call arrived.</summary>
        public long EmitSum { get; private set; }

        /// <summary>Lines echoed by the script.  Bounded so a runaway script cannot grow unbounded.</summary>
        public List<string> LoggedLines { get; } = new();

        public const int MaxLoggedLines = 64;

        public void Reset()
        {
            EmitCount = 0;
            EmitSum = 0;
            LoggedLines.Clear();
            _snapshot = null;
        }

        public void Log(string message)
        {
            // Bounded: a script that logs per iteration must not blow up memory,
            // and nothing here writes to the game log per call.
            if (LoggedLines.Count < MaxLoggedLines)
            {
                LoggedLines.Add(message ?? "<nil>");
            }
        }

        /// <summary>Lua -> C# boundary crossing target for the benchmark.</summary>
        public void Emit(int id)
        {
            EmitCount++;
            EmitSum += id;
        }

        /// <summary>Cheap no-op used to separate marshalling cost from body cost.</summary>
        public void Nop()
        {
        }

        /// <summary>Re-reads the (read-only) player snapshot on the game main thread.</summary>
        public void RefreshPlayerSnapshot()
        {
            _snapshot = null;
        }

        /// <summary>
        /// Returns a string rendering of a read-only player/world field, or null
        /// when the field is genuinely unavailable.  Never returns 0 for
        /// "unknown": a missing world yields null and an explicit reason.
        /// </summary>
        public string PlayerField(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (_snapshot == null)
            {
                _snapshot = _capture();
            }

            return _snapshot.Get(key);
        }

        public bool HasWorld()
        {
            if (_snapshot == null)
            {
                _snapshot = _capture();
            }

            return _snapshot.Available;
        }

        public string Version()
        {
            return LuaProbeVersion.Value;
        }

        public int SchemaVersion()
        {
            return 2;
        }

        // --- CLR enum interop capability probe (see probe_core.enum_probe) ---

        public ProbeReaction SampleReaction()
        {
            return ProbeReaction.Flee;
        }

        public ProbeReaction EchoReaction(ProbeReaction reaction)
        {
            return reaction;
        }
    }

    internal static class LuaProbeVersion
    {
        public const string Value = "LuaRuntimeProbe/1.0.0";
    }

    /// <summary>
    /// Immutable, read-only capture of a few local-player / world fields.
    /// Captured on demand and cached for the duration of one probe run.
    /// </summary>
    public sealed class PlayerSnapshot
    {
        public bool Available;
        public string Reason;
        public string Name;
        public string DisplayName;
        public int Health;
        public int MaxHealth;
        public float Stamina;
        public int GameStage;
        public bool IsDead;
        public bool IsAdmin;
        public float X;
        public float Y;
        public float Z;
        public string WorldGuid;
        public int WorldDay;
        public int WorldHour;
        public int CapturedOnFrame;

        public static PlayerSnapshot Unavailable(string reason)
        {
            return new PlayerSnapshot { Available = false, Reason = reason };
        }

        private static string F(float v)
        {
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>Field lookup used by probe.player_field(key). Null means "not available".</summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            switch (key.ToLowerInvariant())
            {
                case "available":
                    return Available ? "true" : "false";
                case "reason":
                    return Available ? null : (Reason ?? "unavailable");
            }

            if (!Available)
            {
                return null;
            }

            switch (key.ToLowerInvariant())
            {
                case "name":
                    return Name;
                case "display_name":
                    return DisplayName;
                case "health":
                    return Health.ToString(CultureInfo.InvariantCulture);
                case "max_health":
                    return MaxHealth.ToString(CultureInfo.InvariantCulture);
                case "stamina":
                    return F(Stamina);
                case "game_stage":
                    return GameStage.ToString(CultureInfo.InvariantCulture);
                case "is_dead":
                    return IsDead ? "true" : "false";
                case "is_admin":
                    return IsAdmin ? "true" : "false";
                case "pos_x":
                    return F(X);
                case "pos_y":
                    return F(Y);
                case "pos_z":
                    return F(Z);
                case "world_guid":
                    return WorldGuid;
                case "world_day":
                    return WorldDay.ToString(CultureInfo.InvariantCulture);
                case "world_hour":
                    return WorldHour.ToString(CultureInfo.InvariantCulture);
                case "captured_on_frame":
                    return CapturedOnFrame.ToString(CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> AsPairs()
        {
            yield return new KeyValuePair<string, string>("available", Available ? "true" : "false");
            if (!Available)
            {
                yield return new KeyValuePair<string, string>("reason", Reason ?? "unavailable");
                yield break;
            }

            yield return new KeyValuePair<string, string>("name", Name);
            yield return new KeyValuePair<string, string>("display_name", DisplayName);
            yield return new KeyValuePair<string, string>("health", Health.ToString(CultureInfo.InvariantCulture));
            yield return new KeyValuePair<string, string>("max_health", MaxHealth.ToString(CultureInfo.InvariantCulture));
            yield return new KeyValuePair<string, string>("stamina", F(Stamina));
            yield return new KeyValuePair<string, string>("game_stage", GameStage.ToString(CultureInfo.InvariantCulture));
            yield return new KeyValuePair<string, string>("is_dead", IsDead ? "true" : "false");
            yield return new KeyValuePair<string, string>("is_admin", IsAdmin ? "true" : "false");
            yield return new KeyValuePair<string, string>("pos", F(X) + "," + F(Y) + "," + F(Z));
            yield return new KeyValuePair<string, string>("world_guid", WorldGuid);
            yield return new KeyValuePair<string, string>("world_day", WorldDay.ToString(CultureInfo.InvariantCulture));
            yield return new KeyValuePair<string, string>("world_hour", WorldHour.ToString(CultureInfo.InvariantCulture));
            yield return new KeyValuePair<string, string>("captured_on_frame", CapturedOnFrame.ToString(CultureInfo.InvariantCulture));
        }

        public ProbePlayerDto ToDto()
        {
            return new ProbePlayerDto
            {
                Available = Available,
                Reason = Reason,
                Name = Name,
                DisplayName = DisplayName,
                Health = Health,
                MaxHealth = MaxHealth,
                Stamina = Stamina,
                GameStage = GameStage,
                IsDead = IsDead,
                IsAdmin = IsAdmin,
                X = X,
                Y = Y,
                Z = Z,
                WorldGuid = WorldGuid,
                WorldDay = WorldDay,
                WorldHour = WorldHour,
                CapturedOnFrame = CapturedOnFrame,
            };
        }
    }
}
