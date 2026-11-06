using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using LuaRuntimeProbe.Backends;
using LuaRuntimeProbe.Contracts;

namespace LuaRuntimeProbe.Harness
{
    /// <summary>One named assertion.  Passed == null means "informational".</summary>
    public sealed class ProbeCheck
    {
        public string Name;
        public bool? Passed;
        public string Expected;
        public string Actual;
        public string Detail;

        public string Render()
        {
            string status = Passed.HasValue ? (Passed.Value ? "PASS" : "FAIL") : "INFO";
            string text = string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", status, Name);
            if (!string.IsNullOrEmpty(Expected) || !string.IsNullOrEmpty(Actual))
            {
                text += " expected=" + (Expected ?? "-") + " actual=" + (Actual ?? "-");
            }

            if (!string.IsNullOrEmpty(Detail))
            {
                text += " :: " + Detail;
            }

            return text;
        }
    }

    public sealed class ProbeSuiteResult
    {
        public string BackendId;
        public readonly List<ProbeCheck> Checks = new();
        public string InitError;
        public bool Ran;
        public int PassedCount;
        public int FailedCount;
        public int InfoCount;
        public PlayerSnapshot Before;
        public PlayerSnapshot After;

        public void Tally()
        {
            PassedCount = 0;
            FailedCount = 0;
            InfoCount = 0;

            foreach (ProbeCheck check in Checks)
            {
                if (!check.Passed.HasValue)
                {
                    InfoCount++;
                }
                else if (check.Passed.Value)
                {
                    PassedCount++;
                }
                else
                {
                    FailedCount++;
                }
            }
        }
    }

    /// <summary>
    /// The functional probe suite.  Every backend runs exactly the same checks
    /// against the same Lua sources and the same dataset.
    ///
    /// The suite never mutates the caller's backend permanently: it leaves the
    /// backend initialised with both modules loaded, which is also what the
    /// benchmark expects.
    /// </summary>
    public static class ProbeSuite
    {
        public const string CoreModule = "probe_core";
        public const string HelperModule = "probe_helpers";

        public static ProbeSuiteResult Run(ILuaProbeBackend backend, bool includeCapabilities)
        {
            var result = new ProbeSuiteResult { BackendId = backend.Info.Id };
            EvaluateCallCount = 0;

            // Backends survive across commands and worlds; drop any stale snapshot
            // before this run reads the player context.
            backend.InvalidatePlayerSnapshot();
            result.Before = ProbeHost.Current.CapturePlayer();

            if (!backend.IsAvailable)
            {
                Add(result, "backend_available", null, "available", "unavailable", backend.UnavailableReason);
                result.After = ProbeHost.Current.CapturePlayer();
                result.Tally();
                return result;
            }

            string error;

            // ---- lifecycle -------------------------------------------------
            bool initialized = backend.Initialize(out error);
            Add(result, "init", initialized, "true", initialized.ToString(), initialized ? backend.Diagnostics() : error);
            if (!initialized)
            {
                result.InitError = error;
                result.After = ProbeHost.Current.CapturePlayer();
                result.Tally();
                return result;
            }

            result.Ran = true;

            string secondError;
            bool secondInit = backend.Initialize(out secondError);
            Add(result, "init_idempotent", secondInit, "true", secondInit.ToString(),
                secondInit ? "second Initialize() on the same instance returned true" : secondError);

            // ---- module loading -------------------------------------------
            string loadError = null;
            bool helpersOk = backend.LoadScriptFile("Lua/probe_helpers.lua", HelperModule, out loadError);
            Add(result, "load_helpers_module", helpersOk, "true", helpersOk.ToString(),
                helpersOk ? "Lua/probe_helpers.lua loaded into global '" + HelperModule + "'" : loadError);
            if (!helpersOk)
            {
                Finish(result, backend);
                return result;
            }

            loadError = null;
            bool coreOk = backend.LoadScriptFile("Lua/probe_core.lua", CoreModule, out loadError);
            Add(result, "load_core_module", coreOk, "true", coreOk.ToString(),
                coreOk ? "Lua/probe_core.lua loaded into global '" + CoreModule + "'" : loadError);
            if (!coreOk)
            {
                Finish(result, backend);
                return result;
            }

            LuaValue info = Call(backend, "info", null, result, "module_info_shape");
            Add(result, "module_info_schema_version", IsNumberEqual(backend, info, "schema_version", 2, result, out string infoDetail),
                "schema_version=2", infoDetail ?? "<table>", null);

            // ---- C# -> Lua, primitive round trips --------------------------
            AssertCall(result, backend, "csharp_to_lua_number", "add", new object[] { 2, 3 }, "5");
            AssertCall(result, backend, "csharp_to_lua_float", "add", new object[] { 1.5, 2.25 }, "3.75");
            AssertCall(result, backend, "csharp_to_lua_string", "concat", new object[] { "abc", "def" }, "\"abcdef\"");
            AssertCall(result, backend, "csharp_to_lua_bool", "negate", new object[] { true }, "false");

            // ---- value semantics ------------------------------------------
            LuaValue scalars = Call(backend, "scalars", null, result, "scalars_call");
            AssertField(result, backend, scalars, "scalar_int", "an_int", "42");
            AssertField(result, backend, scalars, "scalar_float", "a_float", "2.5");
            AssertField(result, backend, scalars, "scalar_negative", "a_negative", "-7");
            AssertField(result, backend, scalars, "scalar_bool_true", "a_bool_t", "true");
            AssertField(result, backend, scalars, "scalar_bool_false", "a_bool_f", "false");
            AssertField(result, backend, scalars, "scalar_string", "a_string", "\"hello\"");
            AssertField(result, backend, scalars, "scalar_nil_is_nil", "a_nil", "nil");
            AssertField(result, backend, scalars, "scalar_enum_none", "enum_none", "0");
            AssertField(result, backend, scalars, "scalar_enum_flee", "enum_flee", "1");

            LuaValue nilVsZero = Call(backend, "nil_vs_zero", null, result, "nil_vs_zero_call");
            AssertField(result, backend, nilVsZero, "nil_vs_zero_zero", "zero", "0");
            AssertField(result, backend, nilVsZero, "nil_vs_zero_empty_string", "empty_string", "\"\"");
            AssertField(result, backend, nilVsZero, "nil_vs_zero_false", "false_value", "false");
            AssertField(result, backend, nilVsZero, "nil_vs_zero_missing_key", "not_present_anywhere", "nil");

            // ---- Lua -> C# callbacks --------------------------------------
            var bridge = new ProbeBridge(ProbeHost.Current.CapturePlayer);
            LuaValue callback = LuaValue.Nil();
            try
            {
                bridge.Reset();

                var host = backend as IProbeBridgeHost;
                if (host == null)
                {
                    Add(result, "bridge_reinstall", false, "true", "false",
                        "backend does not implement IProbeBridgeHost, so a counting bridge cannot be installed");
                }
                else
                {
                    host.SetBridge(bridge);
                    Add(result, "bridge_reinstall", true, "true", "true",
                        "counting bridge installed without recreating the Lua state");

                    // Reset AFTER installation: the install-time visibility probe
                    // itself calls probe:Emit(1) to prove the member is callable.
                    bridge.Reset();

                    callback = Call(backend, "callback_probe", new object[] { 5 }, result, "callback_probe_call");
                    Add(result, "lua_to_csharp_return_value", IsInteger(callback, 15), "15", callback.Display,
                        "probe_core.callback_probe(5) must return 1+2+3+4+5");
                    Add(result, "lua_to_csharp_callback_count", bridge.EmitCount == 5, "5",
                        bridge.EmitCount.ToString(CultureInfo.InvariantCulture),
                        "probe.emit() must have been invoked once per iteration");
                    Add(result, "lua_to_csharp_callback_sum", bridge.EmitSum == 15, "15",
                        bridge.EmitSum.ToString(CultureInfo.InvariantCulture),
                        "sum of ids delivered across the boundary");
                }
            }
            catch (Exception e)
            {
                Add(result, "lua_to_csharp_callback_count", false, "5", "exception", Describe(e));
            }

            // ---- structured / nested dataset ------------------------------
            ProbeContextDto standard = ProbeContextDto.CreateStandard();
            LuaValue nestedSum = Call(backend, "sum_ids", new object[] { standard }, result, "sum_ids_call");
            long expectedSum = ExpectedSumIds(standard);
            Add(result, "nested_traversal_checksum", IsInteger(nestedSum, expectedSum),
                expectedSum.ToString(CultureInfo.InvariantCulture), nestedSum.Display,
                "coverage: event id, each tag string length, floor(tags.threat*100), actor name length, "
                + "actor.hostile (+1000) and actor.level, for the standard dataset's ASCII values. "
                + "scenario, kind and the player sub-table are NOT part of this checksum.");

            AssertEvaluate(result, backend, "reaction_standard", standard, "\"Engage\"");
            AssertEvaluate(result, backend, "reaction_flee", ProbeContextDto.CreateFlee(), "\"Flee\"");
            AssertEvaluate(result, backend, "reaction_reload", ProbeContextDto.CreateReload(), "\"Reload\"");
            AssertEvaluate(result, backend, "reaction_no_match_is_nil", ProbeContextDto.CreateNonMatching(), "nil");
            AssertEvaluate(result, backend, "reaction_empty_is_nil", ProbeContextDto.CreateEmpty(), "nil");

            // ---- deliberate failures --------------------------------------
            string runtimeMessage;
            backend.CallFunction(CoreModule, "error_probe", null, out runtimeMessage);
            Add(result, "runtime_error_is_readable",
                !string.IsNullOrEmpty(runtimeMessage) && runtimeMessage.IndexOf("INTENTIONAL_RUNTIME_ERROR", StringComparison.Ordinal) >= 0,
                "message containing INTENTIONAL_RUNTIME_ERROR", Truncate(runtimeMessage),
                "deliberate error, invoked exactly once per run so it cannot spam the log");

            string fileError;
            bool brokenFile = backend.LoadScriptFile("Lua/errors/runtime_error.lua", "probe_error_module", out fileError);
            Add(result, "runtime_error_file_fails", !brokenFile && !string.IsNullOrEmpty(fileError),
                "false", brokenFile.ToString(), Truncate(fileError));

            string syntaxError;
            bool badSyntax = backend.LoadScriptFile("Lua/errors/syntax_error.lua", "probe_syntax_module", out syntaxError);
            Add(result, "syntax_error_file_fails", !badSyntax && !string.IsNullOrEmpty(syntaxError),
                "false", badSyntax.ToString(), Truncate(syntaxError));
            Add(result, "syntax_error_names_chunk", null, "chunk name in message", null,
                "message: " + Truncate(syntaxError));

            // ---- module instance reuse ------------------------------------
            LuaValue callCount = Call(backend, "call_count", null, result, "call_count_call");
            Add(result, "module_state_is_reused", IsInteger(callCount, EvaluateCallCount),
                EvaluateCallCount.ToString(CultureInfo.InvariantCulture), callCount.Display,
                "probe_core keeps call state, proving the loaded module instance is reused across calls");

            // ---- read-only game context -----------------------------------
            RunReadOnlyChecks(result, backend);

            // ---- bridge surface (unconditional: see RunBridgeSurfaceCheck) --
            RunBridgeSurfaceCheck(result);

            // ---- capability probes ----------------------------------------
            if (includeCapabilities)
            {
                RunCapabilityChecks(result, backend);
            }

            // ---- user script path (exactly what `luaprobe run` does) --------
            string userLoadError;
            bool userLoaded = backend.LoadScriptFile("Lua/user_demo.lua", "user_module", out userLoadError);
            Add(result, "user_script_loads", userLoaded, "true", userLoaded.ToString(),
                userLoaded ? "Lua/user_demo.lua loaded as user_module" : userLoadError);

            if (userLoaded)
            {
                string userCallError;
                LuaValue userValue = backend.CallFunction("user_module", "main", new object[] { "alpha", "beta" }, out userCallError);
                bool userOk = string.IsNullOrEmpty(userCallError)
                              && userValue != null
                              && userValue.IsString
                              && userValue.Text != null
                              && userValue.Text.IndexOf("user_demo", StringComparison.Ordinal) >= 0;

                Add(result, "user_script_main_callable", userOk, "string containing user_demo",
                    string.IsNullOrEmpty(userCallError) ? userValue.Display : "error",
                    userCallError ?? "the luaprobe run code path works for this backend");
            }
            else
            {
                Add(result, "user_script_main_callable", false, "string containing user_demo", "not attempted",
                    "module failed to load, so main() could not be called");
            }

            // ---- lifecycle round trip -------------------------------------
            backend.Shutdown();
            string reinitError;
            bool reinit = backend.Initialize(out reinitError);
            Add(result, "reinit_after_shutdown", reinit, "true", reinit.ToString(), reinit ? "clean re-initialisation" : reinitError);

            if (reinit)
            {
                string reloadError;
                bool reloaded = backend.LoadScriptFile("Lua/probe_helpers.lua", HelperModule, out reloadError)
                                && backend.LoadScriptFile("Lua/probe_core.lua", CoreModule, out reloadError);
                Add(result, "reload_modules_after_reinit", reloaded, "true", reloaded.ToString(),
                    reloaded ? "modules reloaded after re-init" : reloadError);

                AssertCall(result, backend, "usable_after_reinit", "add", new object[] { 20, 22 }, "42");
            }

            Finish(result, backend);
            return result;
        }

        private static int EvaluateCallCount;

        // ------------------------------------------------------------------
        // check groups
        // ------------------------------------------------------------------

        private static void RunReadOnlyChecks(ProbeSuiteResult result, ILuaProbeBackend backend)
        {
            LuaValue snapshot = Call(backend, "player_snapshot", null, result, "player_snapshot_call");

            if (snapshot == null || snapshot.Kind != LuaValueKind.Table)
            {
                Add(result, "readonly_player_context", false, "table",
                    snapshot == null ? "null" : snapshot.KindName,
                    "probe_core.player_snapshot() must return a table");
                return;
            }

            // probe:PlayerField(key) hands back STRINGS (or nil); probe:HasWorld() hands
            // back a real Lua boolean.  The branch is therefore decided on the boolean,
            // and every field is checked against the type the bridge actually produces.
            // An earlier revision tested `available` for a Lua boolean - a check that can
            // never be true - which silently made the "world available" branch dead code
            // and let the unavailable branch pass without inspecting the reason.
            LuaValue hasWorld = backend.TableGet(snapshot, "has_world", out string hasWorldError);
            LuaValue available = backend.TableGet(snapshot, "available", out _);
            LuaValue reason = backend.TableGet(snapshot, "reason", out _);

            if (!string.IsNullOrEmpty(hasWorldError) || hasWorld == null || !hasWorld.IsBoolean)
            {
                Add(result, "readonly_player_context", false, "boolean has_world",
                    hasWorldError ?? (hasWorld == null ? "null" : hasWorld.KindName),
                    "has_world must be a Lua boolean produced by probe:HasWorld()");
                return;
            }

            if (hasWorld.Bool)
            {
                LuaValue name = backend.TableGet(snapshot, "name", out _);
                LuaValue health = backend.TableGet(snapshot, "health", out _);
                LuaValue maxHealth = backend.TableGet(snapshot, "max_health", out _);
                LuaValue frame = backend.TableGet(snapshot, "captured_on_frame", out _);

                bool ok = IsNonEmptyString(name)
                          && IsNumericString(health)
                          && IsNumericString(maxHealth)
                          && IsNumericString(frame)
                          && available != null && available.IsString
                          && string.Equals(available.Text, "true", StringComparison.Ordinal);

                Add(result, "readonly_player_context", ok,
                    "has_world=true, available=\"true\", non-empty name, numeric health/max_health/captured_on_frame",
                    "name=" + name.Display + " health=" + health.Display
                    + " max_health=" + maxHealth.Display + " captured_on_frame=" + frame.Display
                    + " available=" + available.Display,
                    "captured on game frame " + result.Before.CapturedOnFrame
                    + " via GameManager.Instance.World.GetPrimaryPlayer()");
                return;
            }

            string reasonText = reason != null && reason.IsString ? reason.Text : null;

            bool legitimatelyUnavailable = !string.IsNullOrEmpty(reasonText)
                && (reasonText.IndexOf("world_unavailable", StringComparison.Ordinal) >= 0
                    || reasonText.IndexOf("player_unavailable", StringComparison.Ordinal) >= 0);

            bool contextError = !string.IsNullOrEmpty(reasonText)
                && reasonText.IndexOf("context_error", StringComparison.Ordinal) >= 0;

            Add(result, "readonly_player_context", legitimatelyUnavailable && !contextError,
                "has_world=false with a world_unavailable/player_unavailable reason (context_error is a failure)",
                "has_world=false reason=" + (reasonText ?? "<nil>"),
                "explicit unavailability is required when no world is loaded; nothing is faked as 0");
        }

        /// <summary>
        /// Structural check of the bridge declaration.
        ///
        /// This is deliberately a SEPARATE step called unconditionally from Run().
        /// It used to live at the end of RunReadOnlyChecks, after the world-available
        /// branch's early return - so it silently disappeared whenever a world WAS
        /// loaded, i.e. in exactly the case that matters most.
        /// </summary>
        private static void RunBridgeSurfaceCheck(ProbeSuiteResult result)
        {
            var mutators = new List<string>();
            foreach (PropertyInfo p in typeof(ProbeBridge).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetSetMethod(false) != null)
                {
                    mutators.Add("property " + p.Name + " has a public setter");
                }
            }

            foreach (FieldInfo f in typeof(ProbeBridge).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!f.IsInitOnly)
                {
                    mutators.Add("public mutable field " + f.Name);
                }
            }

            foreach (MethodInfo m in typeof(ProbeBridge).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (m.IsSpecialName)
                {
                    continue;
                }

                string lower = m.Name.ToLowerInvariant();
                if (lower.StartsWith("set") || lower.StartsWith("add") || lower.StartsWith("remove")
                    || lower.StartsWith("kill") || lower.StartsWith("damage") || lower.StartsWith("teleport")
                    || lower.StartsWith("spawn") || lower.StartsWith("delete"))
                {
                    mutators.Add("method " + m.Name);
                }
            }

            Add(result, "readonly_bridge_surface_shape", mutators.Count == 0,
                "no public setter, no public mutable field, no mutating method name",
                mutators.Count == 0 ? "none found" : string.Join("; ", mutators.ToArray()),
                "limited STRUCTURAL check of the bridge declaration only. It is not a sandbox and does not "
                + "audit method bodies, return values or inherited members. 'Read-only' here means the reviewed "
                + "game access path (GameContextProvider.Capture) assigns nothing to game objects; the bridge "
                + "deliberately exposes a bounded log list and a counter Reset().");
        }

        private static void RunCapabilityChecks(ProbeSuiteResult result, ILuaProbeBackend backend)
        {
            // require() support
            string requireDetail;
            bool requireOk = backend.RequireProbe("probe_require_probe", out requireDetail);
            Add(result, "capability_require", null, "loaded module table or explicit failure", requireOk.ToString(),
                requireDetail);

            // CLR member access: name resolution and call syntax differ per backend.
            LuaValue bridgeProbe = Call(backend, "bridge_probe", null, result, "bridge_probe_call");
            Add(result, "capability_bridge_member_case", null, "informational", null,
                "type(probe)=" + Field(backend, bridgeProbe, "probe_type")
                + " Emit=" + Field(backend, bridgeProbe, "emit_exact")
                + " emit=" + Field(backend, bridgeProbe, "emit_lower")
                + " PlayerField=" + Field(backend, bridgeProbe, "player_field_exact")
                + " player_field=" + Field(backend, bridgeProbe, "player_field_lower")
                + " || Version()=" + Field(backend, bridgeProbe, "version_dot")
                + " | Version:()=" + Field(backend, bridgeProbe, "version_colon")
                + " || Emit(1)=" + Field(backend, bridgeProbe, "emit_dot")
                + " | Emit:(1)=" + Field(backend, bridgeProbe, "emit_colon")
                + " || PlayerField('health')=" + Field(backend, bridgeProbe, "player_field_dot")
                + " | PlayerField:('health')=" + Field(backend, bridgeProbe, "player_field_colon"));

            // enum interop
            LuaValue enumInfo = Call(backend, "enum_probe", null, result, "enum_probe_call");            string luaType = Field(backend, enumInfo, "lua_type");
            string roundtrip = Field(backend, enumInfo, "roundtrip_type");
            string roundtripError = Field(backend, enumInfo, "roundtrip_error");
            Add(result, "capability_clr_enum", null, "informational", null,
                "sample_reaction() appears in Lua as=" + luaType
                + "; echo_reaction() round trip type=" + (roundtrip ?? "<failed>")
                + (string.IsNullOrEmpty(roundtripError) ? string.Empty : "; error=" + Truncate(roundtripError)));

            // language feature matrix
            LuaValue features = Call(backend, "language_features", null, result, "language_features_call");
            Add(result, "capability_language_features", null, "informational", null,
                "version=" + Field(backend, features, "version")
                + " math.type=" + Field(backend, features, "has_math_type")
                + " utf8=" + Field(backend, features, "has_utf8")
                + " table.pack=" + Field(backend, features, "has_table_pack")
                + " bit32=" + Field(backend, features, "has_bit32")
                + " string.pack=" + Field(backend, features, "has_string_pack")
                + " package=" + Field(backend, features, "has_package")
                + " load=" + Field(backend, features, "has_load")
                + " loadstring=" + Field(backend, features, "has_loadstring")
                + " setfenv=" + Field(backend, features, "has_setfenv")
                + " coroutine.close=" + Field(backend, features, "has_coroutine_close")
                + " warn=" + Field(backend, features, "has_warn")
                + " math.maxinteger=" + Field(backend, features, "math_maxinteger"));

            LuaValue gotoProbe = Call(backend, "goto_compile_probe", null, result, "goto_probe_call");
            Add(result, "capability_goto_compile", null, "informational", null,
                "compiled=" + Field(backend, gotoProbe, "compiled")
                + " error=" + Truncate(Field(backend, gotoProbe, "error_text")));

            LuaValue intdivProbe = Call(backend, "intdiv_compile_probe", null, result, "intdiv_probe_call");
            Add(result, "capability_intdiv_compile", null, "informational", null,
                "compiled=" + Field(backend, intdivProbe, "compiled")
                + " executed=" + Field(backend, intdivProbe, "executed")
                + " value=" + Field(backend, intdivProbe, "value")
                + " error=" + Truncate(Field(backend, intdivProbe, "error_text")));

            // The embedding library's version and the Lua LANGUAGE level are
            // different facts; MoonSharp reports _VERSION as "MoonSharp 2.0.0.0",
            // so the language level is derived from feature probes, not assumed.
            LuaValue closeProbe = Call(backend, "close_attrib_probe", null, result, "close_attrib_probe_call");
            Add(result, "capability_close_attrib_compile", null, "informational", null,
                "local x <close> = ... compiled=" + Field(backend, closeProbe, "compiled")
                + " error=" + Truncate(Field(backend, closeProbe, "error_text"))
                + " (5.4-only syntax)");

            Add(result, "capability_measured_language_level", null, "informational",
                InferLanguageLevel(backend, features, intdivProbe, closeProbe),
                "embed version=" + backend.Info.EmbedVersion
                + "; _VERSION=" + (backend.Info.MeasuredLuaVersion ?? "<unknown>")
                + "; declared=" + backend.Info.DeclaredLuaLanguageVersion);
        }

        /// <summary>
        /// Derives the Lua language level from observable feature support rather
        /// than from the embedding library's version string.
        ///
        /// IMPORTANT: `//`, `math.type`, `utf8` and `string.pack` exist in Lua 5.3
        /// already, so on their own they only prove "5.3 or newer".  A 5.4 claim
        /// additionally requires a genuinely 5.4-only signal: the to-be-closed
        /// `<close>` attribute, `coroutine.close`, or `warn`.
        /// </summary>
        private static string InferLanguageLevel(ILuaProbeBackend backend, LuaValue features, LuaValue intdiv, LuaValue closeProbe)
        {
            bool hasMathType = FieldEquals(backend, features, "has_math_type", "true");
            bool hasBit32 = FieldEquals(backend, features, "has_bit32", "true");
            bool hasCoroutineClose = FieldEquals(backend, features, "has_coroutine_close", "true");
            bool hasWarn = FieldEquals(backend, features, "has_warn", "true");
            bool closeAttribCompiles = FieldEquals(backend, closeProbe, "compiled", "true");
            bool intdivCompiled = FieldEquals(backend, intdiv, "compiled", "true");

            if (closeAttribCompiles || hasCoroutineClose || hasWarn)
            {
                return "5.4 (5.4-only feature present: "
                       + (closeAttribCompiles ? "<close> attribute" : hasCoroutineClose ? "coroutine.close" : "warn")
                       + ")";
            }

            if (intdivCompiled && hasMathType)
            {
                return "5.3 or newer, but no 5.4-only feature detected "
                       + "(// and math.type present; <close>, coroutine.close and warn absent)";
            }

            if (!intdivCompiled && hasBit32)
            {
                return "5.2 (no // operator, bit32 present)";
            }

            if (!intdivCompiled)
            {
                return "pre-5.3 (no // operator)";
            }

            return "indeterminate from the available probes";
        }

        private static bool FieldEquals(ILuaProbeBackend backend, LuaValue table, string key, string expected)
        {
            if (table == null)
            {
                return false;
            }

            return string.Equals(Field(backend, table, key), expected, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        private static LuaValue Call(ILuaProbeBackend backend, string function, object[] args, ProbeSuiteResult result, string checkName)
        {
            string error;
            LuaValue value = backend.CallFunction(CoreModule, function, args, out error);

            if (!string.IsNullOrEmpty(error))
            {
                Add(result, checkName, false, "no error", "error", Truncate(error));
            }

            return value;
        }

        private static void AssertCall(ProbeSuiteResult result, ILuaProbeBackend backend, string name, string function, object[] args, string expected)
        {
            string error;
            LuaValue value = backend.CallFunction(CoreModule, function, args, out error);
            if (!string.IsNullOrEmpty(error))
            {
                Add(result, name, false, expected, "error", Truncate(error));
                return;
            }

            Add(result, name, value.Display == expected, expected, value.Display, null);
        }

        private static void AssertEvaluate(ProbeSuiteResult result, ILuaProbeBackend backend, string name, ProbeContextDto ctx, string expected)
        {
            string error;
            LuaValue value = backend.CallFunction(CoreModule, "evaluate", new object[] { ctx }, out error);
            EvaluateCallCount++;

            if (!string.IsNullOrEmpty(error))
            {
                Add(result, name, false, expected, "error", Truncate(error));
                return;
            }

            Add(result, name, value.Display == expected, expected, value.Display,
                "dataset scenario=" + ctx.Scenario + " events=" + (ctx.Events != null ? ctx.Events.Length : 0));
        }

        private static void AssertField(ProbeSuiteResult result, ILuaProbeBackend backend, LuaValue table, string name, string key, string expected)
        {
            string error;
            LuaValue value = backend.TableGet(table, key, out error);
            if (!string.IsNullOrEmpty(error))
            {
                Add(result, name, false, expected, "error", Truncate(error));
                return;
            }

            Add(result, name, value.Display == expected, expected, value.Display, null);
        }

        private static bool IsNumberEqual(ILuaProbeBackend backend, LuaValue table, string key, double expected, ProbeSuiteResult result, out string detail)
        {
            LuaValue value = backend.TableGet(table, key, out string error);
            detail = value != null ? value.Display : null;
            if (!string.IsNullOrEmpty(error) || value == null || !value.IsNumber)
            {
                return false;
            }

            return Math.Abs(value.Number - expected) < 1e-9;
        }

        private static bool IsInteger(LuaValue value, long expected)
        {
            return value != null && value.IsNumber && Math.Abs(value.Number - expected) < 1e-9;
        }

        private static bool IsNonEmptyString(LuaValue value)
        {
            return value != null && value.IsString && !string.IsNullOrEmpty(value.Text);
        }

        /// <summary>
        /// probe:PlayerField(key) hands numbers back as STRINGS, so a numeric field
        /// must be validated as a parseable string, not as a Lua number.
        /// </summary>
        private static bool IsNumericString(LuaValue value)
        {
            if (value == null || !value.IsString || string.IsNullOrEmpty(value.Text))
            {
                return false;
            }

            double parsed;
            return double.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
        }

        private static string Field(ILuaProbeBackend backend, LuaValue table, string key)
        {
            LuaValue value = backend.TableGet(table, key, out string error);
            if (value == null)
            {
                return error ?? "<null>";
            }

            return value.Display;
        }

        /// <summary>
        /// Independent C# re-implementation of probe_core.sum_ids.  Used so the
        /// checksum is reproducible from code, not hard-coded.
        /// </summary>
        internal static long ExpectedSumIds(ProbeContextDto ctx)
        {
            long total = 0;
            if (ctx == null || ctx.Events == null)
            {
                return total;
            }

            foreach (ProbeEventDto ev in ctx.Events)
            {
                total += ev.Id;

                if (ev.Tags != null)
                {
                    foreach (string tag in ev.Tags)
                    {
                        total += tag.Length;
                    }

                    total += (long)Math.Floor(ev.Threat * 100.0);
                }

                if (ev.Actor != null)
                {
                    total += (ev.Actor.Name ?? string.Empty).Length;
                    if (ev.Actor.Hostile)
                    {
                        total += 1000;
                    }

                    total += ev.Actor.Level;
                }
            }

            return total;
        }

        private static void Finish(ProbeSuiteResult result, ILuaProbeBackend backend)
        {
            result.After = ProbeHost.Current.CapturePlayer();
            result.Tally();
        }

        private static void Add(ProbeSuiteResult result, string name, bool? passed, string expected, string actual, string detail)
        {
            result.Checks.Add(new ProbeCheck
            {
                Name = name,
                Passed = passed,
                Expected = expected,
                Actual = actual,
                Detail = detail,
            });
        }

        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            string single = text.Replace("\r", " ").Replace("\n", " ");
            return single.Length <= 400 ? single : single.Substring(0, 400) + "...";
        }

        private static string Describe(Exception e)
        {
            return e.GetType().Name + ": " + e.Message;
        }
    }
}
