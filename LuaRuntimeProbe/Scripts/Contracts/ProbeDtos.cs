using System.Collections.Generic;

namespace LuaRuntimeProbe.Contracts
{
    /// <summary>
    /// CLR enum used ONLY by the enum-interop capability probe.  The portable
    /// Lua contract deliberately does not depend on CLR enums, because Lua 5.2
    /// (MoonSharp) and Lua 5.4 (KeraLua) represent them differently.
    /// </summary>
    public enum ProbeReaction
    {
        None = 0,
        Flee = 1,
        Engage = 2,
        Reload = 3,
        Investigate = 4,
    }

    /// <summary>Actor node inside a structured event payload.</summary>
    public sealed class ProbeActorDto
    {
        public string Name;
        public bool Hostile;
        public int Level;
    }

    /// <summary>One nested event node.  Tags is a mixed array/hash table in Lua.</summary>
    public sealed class ProbeEventDto
    {
        public int Id;
        public string Kind;
        public string[] Tags;
        public double Threat;
        public ProbeActorDto Actor;
    }

    /// <summary>Read-only player context handed to the script (never written back).</summary>
    public sealed class ProbePlayerDto
    {
        public bool Available;
        public string Reason;
        public string Name;
        public string DisplayName;
        public int Health;
        public int MaxHealth;
        public double Stamina;
        public int GameStage;
        public bool IsDead;
        public bool IsAdmin;
        public double X;
        public double Y;
        public double Z;
        public string WorldGuid;
        public int WorldDay;
        public int WorldHour;
        public int CapturedOnFrame;
    }

    /// <summary>
    /// The full structured dataset handed from C# to Lua.  Marshalling it into
    /// a Lua table is the "DTO / table conversion" cost measured by the bench.
    /// </summary>
    public sealed class ProbeContextDto
    {
        public int SchemaVersion = 2;
        public string Scenario = "default";
        public ProbeEventDto[] Events;
        public ProbePlayerDto Player;

        /// <summary>
        /// The canonical dataset used by every backend so results are comparable.
        /// Deterministic: no randomness, no timestamps.
        /// </summary>
        public static ProbeContextDto CreateStandard()
        {
            return new ProbeContextDto
            {
                SchemaVersion = 2,
                Scenario = "standard",
                Events = new[]
                {
                    new ProbeEventDto
                    {
                        Id = 1,
                        Kind = "enemy_seen",
                        Tags = new[] { "enemy", "hostile" },
                        Threat = 0.30,
                        Actor = new ProbeActorDto { Name = "Zombie", Hostile = true, Level = 3 },
                    },
                    new ProbeEventDto
                    {
                        Id = 2,
                        Kind = "weapon_state",
                        Tags = new[] { "firing" },
                        Threat = 0.10,
                        Actor = new ProbeActorDto { Name = "Player", Hostile = false, Level = 12 },
                    },
                    new ProbeEventDto
                    {
                        Id = 3,
                        Kind = "enemy_seen",
                        Tags = new[] { "enemy" },
                        Threat = 0.42,
                        Actor = new ProbeActorDto { Name = "Zombie", Hostile = true, Level = 5 },
                    },
                },
            };
        }

        /// <summary>Same shape, but every rule must fail => evaluate() must return nil.</summary>
        public static ProbeContextDto CreateNonMatching()
        {
            return new ProbeContextDto
            {
                SchemaVersion = 2,
                Scenario = "no_match",
                Events = new[]
                {
                    new ProbeEventDto
                    {
                        Id = 10,
                        Kind = "ambient",
                        Tags = new[] { "idle" },
                        Threat = 0.05,
                        Actor = new ProbeActorDto { Name = "Player", Hostile = false, Level = 1 },
                    },
                },
            };
        }

        /// <summary>Reloading tag with low threat => evaluate() must return "Reload".</summary>
        public static ProbeContextDto CreateReload()
        {
            return new ProbeContextDto
            {
                SchemaVersion = 2,
                Scenario = "reload",
                Events = new[]
                {
                    new ProbeEventDto
                    {
                        Id = 15,
                        Kind = "weapon_state",
                        Tags = new[] { "reloading" },
                        Threat = 0.20,
                        Actor = new ProbeActorDto { Name = "Player", Hostile = false, Level = 8 },
                    },
                },
            };
        }

        /// <summary>High threat => evaluate() must return "Flee".</summary>
        public static ProbeContextDto CreateFlee()
        {
            return new ProbeContextDto
            {
                SchemaVersion = 2,
                Scenario = "flee",
                Events = new[]
                {
                    new ProbeEventDto
                    {
                        Id = 20,
                        Kind = "enemy_seen",
                        Tags = new[] { "enemy", "hostile" },
                        Threat = 0.91,
                        Actor = new ProbeActorDto { Name = "Feral", Hostile = true, Level = 40 },
                    },
                },
            };
        }

        /// <summary>Empty event list => evaluate() must return nil (no rule fires).</summary>
        public static ProbeContextDto CreateEmpty()
        {
            return new ProbeContextDto
            {
                SchemaVersion = 2,
                Scenario = "empty",
                Events = new ProbeEventDto[0],
            };
        }

        public static IEnumerable<ProbeContextDto> AllStandard()
        {
            yield return CreateStandard();
            yield return CreateNonMatching();
            yield return CreateFlee();
            yield return CreateEmpty();
        }
    }
}
