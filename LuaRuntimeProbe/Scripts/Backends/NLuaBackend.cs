using System;
using System.Collections.Generic;
using System.Globalization;
using LuaRuntimeProbe.Contracts;
using LuaRuntimeProbe.Harness;
using NLua;

namespace LuaRuntimeProbe.Backends
{
    /// <summary>
    /// NLua 1.7.9 + KeraLua 1.4.10: managed binding over the native Lua 5.4
    /// interpreter (lua54.dll).  This is the "native Lua" route: the real
    /// reference implementation, at the cost of a native dependency that must
    /// be side-loaded from the mod folder.
    /// </summary>
    public sealed class NLuaBackend : ILuaProbeBackend, IProbeBridgeHost
    {
        private const string NluaVersion = "1.7.9";
        private const string KeraLuaVersion = "1.4.10";
        private const string NativeFileName = "lua54.dll";
        private const string ScratchPrefix = "__lrp_scratch_";

        private readonly LuaBackendInfo _info = new()
        {
            Id = "nlua",
            DisplayName = "NLua + KeraLua (native Lua)",
            EmbedVersion = "NLua " + NluaVersion + " + KeraLua " + KeraLuaVersion,
            DeclaredLuaLanguageVersion = "5.4",
            RuntimeKind = "native (P/Invoke into " + NativeFileName + ")",
            NativeDependency = "native/" + NativeFileName + " (KeraLua runtimes/win-x64/native/lua54.dll)",
            PackageSource = "nuget.org NLua " + NluaVersion + " (net46 asset) + KeraLua " + KeraLuaVersion
                            + " (net46 asset); native lua54.dll copied from the KeraLua package",
            Notes = "Real Lua 5.4: integer subtype, //, bitwise ops, utf8 library, goto.",
        };

        private Lua _lua;
        private ProbeBridge _bridge;
        private int _scratch;
        private string _nativeLoadDetail;
        private string _bridgeDetail;
        private string _initError;
        private bool _nativeMissing;

        /// <summary>
        /// Resolved function handles, keyed by "module.function".  NLua resolves a
        /// dotted path by walking the global table on every GetFunction() call, so
        /// without this cache the "hot call" benchmark would include that lookup
        /// while claiming to measure a cached function.
        /// </summary>
        private readonly Dictionary<string, LuaFunction> _functionCache = new();

        public LuaBackendInfo Info
        {
            get { return _info; }
        }

        /// <summary>
        /// False only for a PERMANENT failure (the native interpreter is missing).
        /// A transient initialisation failure leaves this true so the next command
        /// retries instead of locking the backend out for the whole session.
        /// </summary>
        public bool IsAvailable
        {
            get { return !_nativeMissing; }
        }

        public string UnavailableReason
        {
            get { return _nativeMissing ? _nativeLoadDetail : _initError; }
        }

        public bool IsInitialized
        {
            get { return _lua != null; }
        }

        /// <summary>Result of the native library preload, for the report.</summary>
        public string NativeLoadDetail
        {
            get { return _nativeLoadDetail; }
        }

        public bool Initialize(out string error)
        {
            error = null;

            if (_lua != null)
            {
                return true;
            }

            if (_nativeMissing)
            {
                error = "native interpreter unavailable (permanent): " + _nativeLoadDetail;
                return false;
            }

            // 1. Side-load the native interpreter BEFORE the first P/Invoke.
            if (!NativeLibraryLoader.TryLoad(ProbeHost.Current.NativeDirectory, NativeFileName, out _nativeLoadDetail))
            {
                _nativeMissing = true;
                _initError = _nativeLoadDetail;
                error = _nativeLoadDetail;
                return false;
            }

            try
            {
                _lua = new Lua();
                _lua.UseTraceback = true;

                _bridge = new ProbeBridge(ProbeHost.Current.CapturePlayer);

                string bridgeDetail;
                if (!InstallBridge(_bridge, out bridgeDetail))
                {
                    throw new InvalidOperationException(bridgeDetail);
                }

                _bridgeDetail = bridgeDetail;

                object[] version = _lua.DoString("return _VERSION", "=version");
                _info.MeasuredLuaVersion = version != null && version.Length > 0 && version[0] is string
                    ? (string)version[0]
                    : "<not reported>";

                // Make `require` find mod-local modules.
                string luaDir = (ProbeHost.Current.LuaDirectory ?? string.Empty).Replace('\\', '/');
                if (luaDir.Length > 0)
                {
                    _lua.DoString(
                        "package.path = package.path .. ';" + luaDir + "/?.lua'",
                        "=package_path_setup");
                }

                _initError = null;
                return true;
            }
            catch (Exception e)
            {
                error = Describe(e);
                _initError = error;
                ShutdownSilently();
                return false;
            }
        }

        public void Shutdown()
        {
            if (_lua == null)
            {
                return;
            }

            try
            {
                _lua.Dispose();
            }
            catch (Exception e)
            {
                ProbeHost.Current.LogWarning("[LuaRuntimeProbe] nlua shutdown: " + Describe(e));
            }

            _lua = null;
            _bridge = null;
            _functionCache.Clear();
        }

        public void InvalidatePlayerSnapshot()
        {
            if (_bridge != null)
            {
                _bridge.RefreshPlayerSnapshot();
            }
        }

        public void ClearRetryableFailure()
        {
            // A missing native library cannot be fixed without a redeploy, so it
            // stays permanent; a transient init failure is cleared so that the
            // next command retries.
            _initError = null;
        }

        public void Tick()
        {
            // NLua's temporary wrappers are released by NLua's own DisposeInternal /
            // finalizer path; Lua 5.4 collects inside the interpreter.  There is no
            // deferred queue to pump here.
        }

        private void ShutdownSilently()
        {
            try
            {
                Shutdown();
            }
            catch (Exception)
            {
                _lua = null;
                _bridge = null;
            }
        }

        /// <summary>
        /// Swaps the bridge that scripts see as the global `probe` without
        /// touching the interpreter state or the loaded modules.
        /// </summary>
        public void SetBridge(ProbeBridge bridge)
        {
            _bridge = bridge;
            if (_lua == null)
            {
                return;
            }

            string detail;
            if (InstallBridge(bridge, out detail))
            {
                _bridgeDetail = detail;
            }
            else
            {
                _bridgeDetail = "SetBridge failed: " + detail;
                ProbeHost.Current.LogWarning("[LuaRuntimeProbe] " + _bridgeDetail);
            }
        }

        /// <summary>
        /// Installs the bridge and VERIFIES that Lua can reach its members.
        /// The canonical NLua indexer is tried first; SetObjectToPath is kept as
        /// a fallback.  Which one worked is recorded for the report.
        /// </summary>
        private bool InstallBridge(ProbeBridge bridge, out string detail)
        {
            var attempts = new List<string>();

            try
            {
                _lua["probe"] = bridge;
                string visible;
                if (VerifyBridge(out visible))
                {
                    detail = "lua[\"probe\"] = bridge (indexer); " + visible;
                    return true;
                }

                attempts.Add("indexer assignment did not expose members (" + visible + ")");
            }
            catch (Exception e)
            {
                attempts.Add("indexer assignment threw " + Describe(e));
            }

            try
            {
                _lua.SetObjectToPath("probe", bridge);
                string visible;
                if (VerifyBridge(out visible))
                {
                    detail = "SetObjectToPath(\"probe\", bridge); " + visible;
                    return true;
                }

                attempts.Add("SetObjectToPath did not expose members (" + visible + ")");
            }
            catch (Exception e)
            {
                attempts.Add("SetObjectToPath threw " + Describe(e));
            }

            detail = "no usable probe bridge could be installed: " + string.Join(" | ", attempts.ToArray());
            return false;
        }

        public bool VerifyBridge(out string detail)
        {
            detail = null;

            if (_lua == null)
            {
                detail = "backend not initialized";
                return false;
            }

            try
            {
                object[] results = _lua.DoString(BridgeVisibility.Snippet, "=bridge_visibility");
                var table = results != null && results.Length > 0 ? results[0] as LuaTable : null;
                if (table == null)
                {
                    detail = "visibility snippet returned "
                             + (results == null || results.Length == 0
                                 ? "nothing"
                                 : results[0] == null ? "nil" : results[0].GetType().Name);
                    return false;
                }

                string probeType = Convert.ToString(table["probe_type"]);
                string emitExact = Convert.ToString(table["emit_exact"]);
                string emitLower = Convert.ToString(table["emit_lower"]);
                string playerFieldExact = Convert.ToString(table["player_field_exact"]);
                string playerFieldLower = Convert.ToString(table["player_field_lower"]);
                string ok = Convert.ToString(table["ok"]);

                detail = "probe=" + probeType
                         + " Emit=" + emitExact + " emit=" + emitLower
                         + " PlayerField=" + playerFieldExact + " player_field=" + playerFieldLower
                         + " | Version()=" + Convert.ToString(table["version_dot"])
                         + " Version:()=" + Convert.ToString(table["version_colon"])
                         + " | Emit(1)=" + Convert.ToString(table["emit_dot"])
                         + " Emit:(1)=" + Convert.ToString(table["emit_colon"])
                         + " | PlayerField('health')=" + Convert.ToString(table["player_field_dot"])
                         + " PlayerField:('health')=" + Convert.ToString(table["player_field_colon"]);

                if (!string.Equals(ok, "True", StringComparison.OrdinalIgnoreCase))
                {
                    detail += " error=" + Convert.ToString(table["error"]);
                    return false;
                }

                // Reachability only: which call style works is a measured
                // capability reported by probe_core.bridge_probe(), not a
                // precondition for installing the bridge.
                return BridgeVisibility.IsReachableType(emitExact)
                       && BridgeVisibility.IsReachableType(playerFieldExact);
            }
            catch (Exception e)
            {
                detail = "visibility snippet threw " + Describe(e);
                return false;
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        public bool LoadScriptFile(string relativePath, string globalName, out string error)
        {
            string readError;
            string source = ProbeHost.Current.ReadModFile(relativePath, out readError);
            if (source == null)
            {
                error = readError;
                return false;
            }

            return LoadScriptText(source, relativePath, globalName, out error);
        }

        public bool LoadScriptText(string source, string chunkName, string globalName, out string error)
        {
            error = null;

            if (_lua == null)
            {
                error = "backend not initialized";
                return false;
            }

            try
            {
                object[] results = _lua.DoString(source, chunkName);
                object module = results != null && results.Length > 0 ? results[0] : null;
                _lua.SetObjectToPath(globalName, module);

                // A module global was replaced, so any cached handle into it is stale.
                _functionCache.Clear();
                return true;
            }
            catch (Exception e)
            {
                error = Describe(e);
                return false;
            }
        }

        public LuaValue CallFunction(string moduleGlobal, string functionName, object[] args, out string error)
        {
            error = null;

            if (_lua == null)
            {
                error = "backend not initialized";
                return LuaValue.Nil();
            }

            try
            {
                LuaFunction function = ResolveFunction(moduleGlobal, functionName, out string lookupError);
                if (function == null)
                {
                    error = lookupError;
                    return LuaValue.Nil();
                }

                object[] results = function.Call(ConvertArguments(args));
                return results != null && results.Length > 0 ? FromObject(results[0]) : LuaValue.Nil();
            }
            catch (Exception e)
            {
                error = Describe(e);
                return LuaValue.Nil();
            }
        }

        private LuaFunction ResolveFunction(string moduleGlobal, string functionName, out string error)
        {
            error = null;
            string key = moduleGlobal + "." + functionName;

            LuaFunction cached;
            if (_functionCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            LuaFunction function = _lua.GetFunction(key);
            if (function == null)
            {
                error = "global '" + key + "' is not a function";
                return null;
            }

            _functionCache[key] = function;
            return function;
        }

        public LuaValue TableGet(LuaValue table, string key, out string error)
        {
            error = null;

            var t = table != null ? table.Raw as LuaTable : null;
            if (t == null)
            {
                error = "value is not a table (kind=" + (table == null ? "null" : table.KindName) + ")";
                return LuaValue.Nil();
            }

            try
            {
                return FromObject(t[key]);
            }
            catch (Exception e)
            {
                error = Describe(e);
                return LuaValue.Nil();
            }
        }

        public LuaValue TableGetInt(LuaValue table, int index, out string error)
        {
            error = null;

            var t = table != null ? table.Raw as LuaTable : null;
            if (t == null)
            {
                error = "value is not a table (kind=" + (table == null ? "null" : table.KindName) + ")";
                return LuaValue.Nil();
            }

            try
            {
                return FromObject(t[(object)index]);
            }
            catch (Exception e)
            {
                error = Describe(e);
                return LuaValue.Nil();
            }
        }

        public bool RequireProbe(string moduleName, out string detail)
        {
            detail = null;

            if (_lua == null)
            {
                detail = "backend not initialized";
                return false;
            }

            try
            {
                object[] results = _lua.DoString("return require('" + moduleName + "')", "=require_probe");
                object value = results != null && results.Length > 0 ? results[0] : null;
                detail = "require('" + moduleName + "') returned " + FromObject(value).Display
                         + " (package.path extended with " + ProbeHost.Current.LuaDirectory + "/?.lua)";
                return true;
            }
            catch (Exception e)
            {
                detail = Describe(e);
                return false;
            }
        }

        public string Diagnostics()
        {
            if (_lua == null)
            {
                return "not initialised" + (_nativeLoadDetail != null ? "; native: " + _nativeLoadDetail : string.Empty);
            }

            return "state alive; _VERSION=" + (_info.MeasuredLuaVersion ?? "?")
                   + "; native: " + (_nativeLoadDetail ?? "<not loaded>")
                   + "; bridge: " + (_bridgeDetail ?? "<not installed>")
                   + (_initError != null ? "; last init error: " + _initError : string.Empty);
        }

        // ------------------------------------------------------------------
        // Marshalling
        // ------------------------------------------------------------------

        private LuaTable NewLuaTable()
        {
            string path = ScratchPrefix + (_scratch++);
            _lua.NewTable(path);
            LuaTable table = _lua.GetTable(path);

            // Release the scratch global; the LuaTable keeps its own registry ref.
            _lua.SetObjectToPath(path, null);
            return table;
        }

        private object[] ConvertArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new object[0];
            }

            var converted = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                converted[i] = ConvertArgument(args[i]);
            }

            return converted;
        }

        private object ConvertArgument(object arg)
        {
            if (arg is ProbeContextDto)
            {
                return BuildContextTable((ProbeContextDto)arg);
            }

            // Primitives, null and strings are converted by NLua's translator.
            return arg;
        }

        private LuaTable BuildContextTable(ProbeContextDto dto)
        {
            LuaTable root = NewLuaTable();
            root["schema_version"] = dto.SchemaVersion;
            root["scenario"] = dto.Scenario ?? string.Empty;

            LuaTable events = NewLuaTable();
            if (dto.Events != null)
            {
                for (int i = 0; i < dto.Events.Length; i++)
                {
                    events[(object)(i + 1)] = BuildEventTable(dto.Events[i]);
                }
            }

            root["events"] = events;
            root["player"] = BuildPlayerTable(dto.Player);
            return root;
        }

        private LuaTable BuildEventTable(ProbeEventDto ev)
        {
            LuaTable t = NewLuaTable();
            t["id"] = ev.Id;
            t["kind"] = ev.Kind ?? string.Empty;

            LuaTable tags = NewLuaTable();
            if (ev.Tags != null)
            {
                for (int i = 0; i < ev.Tags.Length; i++)
                {
                    tags[(object)(i + 1)] = ev.Tags[i];
                }
            }

            tags["threat"] = ev.Threat;
            t["tags"] = tags;

            LuaTable actor = NewLuaTable();
            if (ev.Actor != null)
            {
                actor["name"] = ev.Actor.Name ?? string.Empty;
                actor["hostile"] = ev.Actor.Hostile;
                actor["level"] = ev.Actor.Level;
            }

            t["actor"] = actor;
            return t;
        }

        private LuaTable BuildPlayerTable(ProbePlayerDto p)
        {
            LuaTable t = NewLuaTable();
            if (p == null)
            {
                t["available"] = false;
                t["reason"] = "no player snapshot attached";
                return t;
            }

            t["available"] = p.Available;
            t["reason"] = p.Reason ?? string.Empty;
            t["name"] = p.Name ?? string.Empty;
            t["display_name"] = p.DisplayName ?? string.Empty;
            t["health"] = p.Health;
            t["max_health"] = p.MaxHealth;
            t["stamina"] = p.Stamina;
            t["game_stage"] = p.GameStage;
            t["is_dead"] = p.IsDead;
            t["is_admin"] = p.IsAdmin;
            t["pos_x"] = p.X;
            t["pos_y"] = p.Y;
            t["pos_z"] = p.Z;
            t["world_guid"] = p.WorldGuid ?? string.Empty;
            t["world_day"] = p.WorldDay;
            t["world_hour"] = p.WorldHour;
            t["captured_on_frame"] = p.CapturedOnFrame;
            return t;
        }

        private static LuaValue FromObject(object o)
        {
            if (o == null)
            {
                return LuaValue.Nil();
            }

            if (o is bool)
            {
                return LuaValue.FromBool((bool)o);
            }

            if (o is string)
            {
                return LuaValue.FromString((string)o);
            }

            if (o is LuaTable)
            {
                return LuaValue.FromOther(LuaValueKind.Table, o);
            }

            if (o is LuaFunction)
            {
                return LuaValue.FromOther(LuaValueKind.Function, o);
            }

            if (o is int)
            {
                return LuaValue.FromNumber((int)o, true);
            }

            if (o is long)
            {
                return LuaValue.FromNumber((long)o, true);
            }

            if (o is short)
            {
                return LuaValue.FromNumber((short)o, true);
            }

            if (o is byte)
            {
                return LuaValue.FromNumber((byte)o, true);
            }

            if (o is double)
            {
                return LuaValue.FromNumber((double)o, MoonSharpBackend.IsIntegral((double)o));
            }

            if (o is float)
            {
                return LuaValue.FromNumber((float)o, MoonSharpBackend.IsIntegral((float)o));
            }

            // Enum values and other CLR payloads are classified by the observed
            // runtime type name only, to avoid depending on NLua internals.
            string typeName = o.GetType().Name;
            if (typeName == "LuaUserData")
            {
                return LuaValue.FromOther(LuaValueKind.Userdata, o);
            }

            if (typeName == "LuaThread")
            {
                return LuaValue.FromOther(LuaValueKind.Thread, o);
            }

            if (o is Enum)
            {
                return LuaValue.FromOther(LuaValueKind.Userdata, o);
            }

            return LuaValue.FromOther(LuaValueKind.Other, o);
        }

        private static string Describe(Exception e)
        {
            var parts = new List<string> { e.GetType().Name + ": " + e.Message };

            Exception inner = e.InnerException;
            int depth = 0;
            while (inner != null && depth < 3)
            {
                parts.Add("<- " + inner.GetType().Name + ": " + inner.Message);
                inner = inner.InnerException;
                depth++;
            }

            return string.Join(" ", parts);
        }

        /// <summary>Formats a number for reports without a culture-dependent decimal separator.</summary>
        internal static string Format(double v)
        {
            return v.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
