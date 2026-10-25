using System;
using LuaRuntimeProbe.Contracts;

namespace LuaRuntimeProbe.Backends
{
    /// <summary>Everything a report needs to identify a backend unambiguously.</summary>
    public sealed class LuaBackendInfo
    {
        public string Id;
        public string DisplayName;

        /// <summary>Embedding library identity, e.g. "MoonSharp 2.0.0 (NuGet MoonSharp 2.0.0)".</summary>
        public string EmbedVersion;

        /// <summary>Lua LANGUAGE version the embedding library implements, e.g. "5.2".</summary>
        public string DeclaredLuaLanguageVersion;

        /// <summary>Lua language version actually reported at runtime by _VERSION (measured).</summary>
        public string MeasuredLuaVersion;

        /// <summary>"pure managed (AnyCPU CLR)" or "native (P/Invoke)".</summary>
        public string RuntimeKind;

        /// <summary>Native dependency required, or "none".</summary>
        public string NativeDependency;

        /// <summary>Where the bits came from (package id + version + registry).</summary>
        public string PackageSource;

        public string Notes;

        public override string ToString()
        {
            return Id;
        }
    }

    /// <summary>
    /// One embeddable Lua runtime behind a common, deliberately small contract.
    /// Every backend executes the SAME Lua sources with the SAME dataset.
    /// </summary>
    public interface ILuaProbeBackend : IDisposable
    {
        LuaBackendInfo Info { get; }

        /// <summary>False when the runtime could not be loaded in this process at all.</summary>
        bool IsAvailable { get; }

        /// <summary>Concrete reason when IsAvailable is false. Never empty in that case.</summary>
        string UnavailableReason { get; }

        bool IsInitialized { get; }

        /// <summary>Creates the Lua state and installs the `probe` bridge. Idempotent.</summary>
        bool Initialize(out string error);

        /// <summary>Tears the Lua state down. Must be safe to call repeatedly.</summary>
        void Shutdown();

        /// <summary>
        /// Loads a mod-relative Lua file as a chunk and stores its single return
        /// value under <paramref name="globalName"/>.
        /// </summary>
        bool LoadScriptFile(string relativePath, string globalName, out string error);

        /// <summary>Compiles+executes raw source and stores its return value globally.</summary>
        bool LoadScriptText(string source, string chunkName, string globalName, out string error);

        /// <summary>
        /// Calls <paramref name="moduleGlobal"/>.<paramref name="functionName"/> with
        /// marshalled arguments.  Returns a normalised value; on failure returns
        /// LuaValue.Nil() and sets <paramref name="error"/>.
        /// </summary>
        LuaValue CallFunction(string moduleGlobal, string functionName, object[] args, out string error);

        /// <summary>Reads a string-keyed field from a table LuaValue.</summary>
        LuaValue TableGet(LuaValue table, string key, out string error);

        /// <summary>Reads an integer-indexed element from a table LuaValue.</summary>
        LuaValue TableGetInt(LuaValue table, int index, out string error);

        /// <summary>Attempts `require(name)` and reports the observed outcome.</summary>
        bool RequireProbe(string moduleName, out string detail);

        /// <summary>Short, human-readable backend state for the status report.</summary>
        string Diagnostics();

        /// <summary>
        /// Verifies that scripts can actually reach the members of the global
        /// `probe` object (not merely that a global was assigned).  Returns
        /// false with the observed Lua types when the bridge is unusable.
        /// </summary>
        bool VerifyBridge(out string detail);

        /// <summary>
        /// Drops any cached player snapshot so that the next read re-captures on
        /// the game main thread.  The backends (and therefore their bridges)
        /// survive across console commands and across world changes, so without
        /// this a later command could report a stale player/world snapshot.
        /// </summary>
        void InvalidatePlayerSnapshot();

        /// <summary>
        /// Clears a retryable initialisation failure so the next Initialize()
        /// attempts again.  Permanent failures (a missing native dependency) are
        /// not cleared: they cannot succeed without a redeploy.
        /// </summary>
        void ClearRetryableFailure();

        /// <summary>
        /// Performs the runtime's deferred release work.  xLua releases the native
        /// references behind temporary tables/functions from a finalizer that only
        /// ENQUEUES the work; LuaEnv.Tick() is what performs it, so a long run must tick
        /// instead of relying on process shutdown.  A no-op for the other backends.
        /// </summary>
        void Tick();
    }

    /// <summary>
    /// Implemented by backends that let the probe bridge instance be swapped
    /// in place.  The callback test needs this so it can count boundary
    /// crossings without recreating (and thereby resetting) the Lua state.
    /// </summary>
    public interface IProbeBridgeHost
    {
        void SetBridge(ProbeBridge bridge);
    }

    /// <summary>
    /// Shared Lua snippet that proves whether scripts can really reach the
    /// members of the global `probe`.
    ///
    /// Assigning a global is not proof: NLua accepted the assignment while the
    /// resulting value had no reachable members at all.  The snippet deliberately
    /// probes BOTH the lowercase and the exact CLR spelling, because the
    /// backends disagree about case sensitivity:
    ///   * MoonSharp resolves userdata members case-insensitively;
    ///   * NLua matches member names exactly (case-sensitively).
    /// The portable Lua contract must therefore use the exact CLR names.
    /// </summary>
    internal static class BridgeVisibility
    {
        /// <summary>
        /// Probes, for the active binding layer:
        ///   * which global `probe` is and what its member types look like;
        ///   * whether members resolve with the exact C# spelling or only
        ///     case-insensitively;
        ///   * whether CLR instance methods must be called with `obj.Method(...)`
        ///     or with `obj:Method(...)`.
        ///
        /// Every call is wrapped in pcall, so a failing style is reported as
        /// "err: ..." instead of aborting the probe.
        /// </summary>
        public const string Snippet =
            "local function try(fn)\n" +
            "  local ok, v = pcall(fn)\n" +
            "  local text = tostring(v)\n" +
            "  if string.len(text) > 90 then text = string.sub(text, 1, 90) .. '...' end\n" +
            "  if ok then return 'ok:' .. text end\n" +
            "  return 'err:' .. text\n" +
            "end\n" +
            "local ok, res = pcall(function()\n" +
            "  return { probe = probe, emit_exact = probe.Emit, emit_lower = probe.emit, " +
            "player_field_exact = probe.PlayerField, player_field_lower = probe.player_field, " +
            "sample_exact = probe.SampleReaction, sample_lower = probe.sample_reaction }\n" +
            "end)\n" +
            "if not ok then return { ok = false, error = tostring(res) } end\n" +
            "return {\n" +
            "  ok = true,\n" +
            "  probe_type = type(res.probe),\n" +
            "  emit_exact = type(res.emit_exact),\n" +
            "  emit_lower = type(res.emit_lower),\n" +
            "  player_field_exact = type(res.player_field_exact),\n" +
            "  player_field_lower = type(res.player_field_lower),\n" +
            "  sample_exact = type(res.sample_exact),\n" +
            "  sample_lower = type(res.sample_lower),\n" +
            "  version_dot = try(function() return probe.Version() end),\n" +
            "  version_colon = try(function() return probe:Version() end),\n" +
            "  emit_dot = try(function() probe.Emit(1)\n return 'called' end),\n" +
            "  emit_colon = try(function() probe:Emit(1)\n return 'called' end),\n" +
            "  player_field_dot = try(function() return probe.PlayerField('health') end),\n" +
            "  player_field_colon = try(function() return probe:PlayerField('health') end),\n" +
            "}\n";

        /// <summary>
        /// True when a member reference is usable at all.  NLua exposes CLR
        /// methods as userdata carrying a __call metamethod, so requiring Lua's
        /// "function" type would wrongly reject a perfectly usable binding.
        /// </summary>
        public static bool IsReachableType(string memberType)
        {
            return !string.IsNullOrEmpty(memberType) && memberType != "nil";
        }
    }
}
