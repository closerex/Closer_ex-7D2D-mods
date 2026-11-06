using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LuaRuntimeProbe.Contracts;
using LuaRuntimeProbe.Harness;

#if LUA_PROBE_XLUA
using XLua;
#endif

namespace LuaRuntimeProbe.Backends
{
#if LUA_PROBE_XLUA
    /// <summary>
    /// xLua as a full peer of the MoonSharp and NLua backends.
    ///
    /// The managed half is xLua's OWN General configuration (AssemblyName XLua.Mini,
    /// DefineConstants XLUA_GENERAL, references System + System.Core only) built from
    /// the tagged sources by tools/Build-XLuaGeneral.ps1 - upstream sources are NOT
    /// modified, only the vendor's own compile-time switch is used.  The native half is
    /// the matching prebuilt xlua.dll, side-loaded from native\ before the first
    /// P/Invoke.
    ///
    /// Two integration hazards were found while assessing this route and both are
    /// avoided here:
    ///   * the managed assembly is NOT called XLua.dll, so it cannot shadow the native
    ///     xlua.dll for [DllImport("xlua")] (Windows lookup is case-insensitive);
    ///   * the native library lives in native\, never in the mod root, because 7DTD's
    ///     Mod.LoadAssemblies() loads every *.dll in the root and a native DLL there
    ///     aborts the whole mod.
    /// </summary>
    public sealed class XLuaBackend : ILuaProbeBackend, IProbeBridgeHost
    {
        private const string NativeFileName = "xlua.dll";

        private readonly LuaBackendInfo _info = new LuaBackendInfo
        {
            Id = "xlua",
            DisplayName = "xLua (official General build)",
            EmbedVersion = "XLua.Mini " + XLuaAssessment.SourceRef,
            DeclaredLuaLanguageVersion = "5.4",
            RuntimeKind = "native (P/Invoke into xlua.dll) + managed reflection layer",
            NativeDependency = XLuaAssessment.NativeLibrary,
            PackageSource = "GitHub Tencent/xLua " + XLuaAssessment.SourceRef
                            + " (no NuGet package); managed half compiled from source via the official "
                            + "General/vs2013/XLua.Mini.csproj configuration",
            Notes = "LuaCallCSharp wrappers are emitted at runtime by reflection; no Unity Editor code generation needed.",
        };

        private LuaEnv _env;
        private ProbeBridge _bridge;
        private string _nativeLoadDetail;
        private string _bridgeDetail;
        private string _initError;
        private bool _nativeMissing;

        /// <summary>Resolved function handles; xLua's Get() walks the table on every call.</summary>
        private readonly Dictionary<string, LuaFunction> _functionCache = new Dictionary<string, LuaFunction>();

        public LuaBackendInfo Info
        {
            get { return _info; }
        }

        /// <summary>False only for the permanent failure: the native interpreter is missing.</summary>
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
            get { return _env != null; }
        }

        public bool Initialize(out string error)
        {
            error = null;

            if (_env != null)
            {
                return true;
            }

            if (_nativeMissing)
            {
                error = "native interpreter unavailable (permanent): " + _nativeLoadDetail;
                return false;
            }

            if (!NativeLibraryLoader.TryLoad(ProbeHost.Current.NativeDirectory, NativeFileName, out _nativeLoadDetail))
            {
                _nativeMissing = true;
                _initError = _nativeLoadDetail;
                error = _nativeLoadDetail;
                return false;
            }

            try
            {
                _env = new LuaEnv();

                // `require` support, rooted at <mod>\Lua.  xLua's own searcher
                // LoadFromCustomLoaders is registered unconditionally, including in the
                // General build, so a custom loader is all that is needed.
                string luaDir = ProbeHost.Current.LuaDirectory;
                if (!string.IsNullOrEmpty(luaDir))
                {
                    _env.AddLoader(BuildModLoader(luaDir));
                }

                _bridge = new ProbeBridge(ProbeHost.Current.CapturePlayer);

                string bridgeDetail;
                if (!InstallBridge(_bridge, out bridgeDetail))
                {
                    throw new InvalidOperationException(bridgeDetail);
                }

                _bridgeDetail = bridgeDetail;

                object[] version = _env.DoString("return _VERSION", "=version", null);
                _info.MeasuredLuaVersion = version != null && version.Length > 0 && version[0] is string
                    ? (string)version[0]
                    : "<not reported>";

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

        private static LuaEnv.CustomLoader BuildModLoader(string luaDirectory)
        {
            return delegate(ref string filepath)
            {
                try
                {
                    string relative = (filepath ?? string.Empty).Replace('.', Path.DirectorySeparatorChar);
                    string full = Path.Combine(luaDirectory, relative + ".lua");
                    if (!File.Exists(full))
                    {
                        return null;
                    }

                    filepath = full;
                    return File.ReadAllBytes(full);
                }
                catch (Exception)
                {
                    return null;
                }
            };
        }

        public void Shutdown()
        {
            if (_env == null)
            {
                return;
            }

            try
            {
                _env.Dispose();
            }
            catch (Exception e)
            {
                ProbeHost.Current.LogWarning("[LuaRuntimeProbe] xlua shutdown: " + Describe(e));
            }

            _env = null;
            _bridge = null;
            _functionCache.Clear();
        }

        private void ShutdownSilently()
        {
            try
            {
                Shutdown();
            }
            catch (Exception)
            {
                _env = null;
                _bridge = null;
            }
        }

        public void Dispose()
        {
            Shutdown();
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
            _initError = null;
        }

        public void Tick()
        {
            // xLua's LuaBase finalizer only ENQUEUES the native release; LuaEnv.Tick()
            // performs it.  Temporary tables produced by every DTO push are released
            // through this path, so a benchmark run must tick.
            if (_env != null)
            {
                _env.Tick();
            }
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

            if (_env == null)
            {
                error = "backend not initialized";
                return false;
            }

            try
            {
                object[] results = _env.DoString(source, chunkName, null);
                object module = results != null && results.Length > 0 ? results[0] : null;

                if (module == null)
                {
                    error = "chunk '" + chunkName + "' returned nothing; a probe module must return a table";
                    return false;
                }

                _env.Global.Set(globalName, module);

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

            if (_env == null)
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

            object module = _env.Global.Get<object>(moduleGlobal);
            var table = module as LuaTable;
            if (table == null)
            {
                error = "global '" + moduleGlobal + "' is not a table (got "
                        + (module == null ? "nil" : module.GetType().Name) + ")";
                return null;
            }

            object function = table.Get<object>(functionName);
            var typed = function as LuaFunction;
            if (typed == null)
            {
                error = "global '" + key + "' is not a function (got "
                        + (function == null ? "nil" : function.GetType().Name) + ")";
                return null;
            }

            _functionCache[key] = typed;
            return typed;
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
                return FromObject(t.Get<object>(key));
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
                return FromObject(t.Get<int, object>(index));
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

            if (_env == null)
            {
                detail = "backend not initialized";
                return false;
            }

            try
            {
                object[] results = _env.DoString("return require('" + moduleName + "')", "=require_probe", null);
                object value = results != null && results.Length > 0 ? results[0] : null;
                detail = "require('" + moduleName + "') returned " + FromObject(value).Display
                         + " (custom loader rooted at " + ProbeHost.Current.LuaDirectory + ")";
                return true;
            }
            catch (Exception e)
            {
                detail = Describe(e);
                return false;
            }
        }

        public bool VerifyBridge(out string detail)
        {
            detail = null;

            if (_env == null)
            {
                detail = "backend not initialized";
                return false;
            }

            try
            {
                object[] results = _env.DoString(BridgeVisibility.Snippet, "=bridge_visibility", null);
                var table = results != null && results.Length > 0 ? results[0] as LuaTable : null;
                if (table == null)
                {
                    detail = "visibility snippet returned "
                             + (results == null || results.Length == 0
                                 ? "nothing"
                                 : results[0] == null ? "nil" : results[0].GetType().Name);
                    return false;
                }

                string probeType = Str(table.Get<object>("probe_type"));
                string emitExact = Str(table.Get<object>("emit_exact"));
                string emitLower = Str(table.Get<object>("emit_lower"));
                string playerFieldExact = Str(table.Get<object>("player_field_exact"));
                string playerFieldLower = Str(table.Get<object>("player_field_lower"));
                string ok = Str(table.Get<object>("ok"));

                detail = "probe=" + probeType
                         + " Emit=" + emitExact + " emit=" + emitLower
                         + " PlayerField=" + playerFieldExact + " player_field=" + playerFieldLower
                         + " | Version()=" + Str(table.Get<object>("version_dot"))
                         + " Version:()=" + Str(table.Get<object>("version_colon"))
                         + " | Emit(1)=" + Summarize(Str(table.Get<object>("emit_dot")))
                         + " Emit:(1)=" + Summarize(Str(table.Get<object>("emit_colon")))
                         + " | PlayerField('health')=" + Summarize(Str(table.Get<object>("player_field_dot")))
                         + " PlayerField:('health')=" + Summarize(Str(table.Get<object>("player_field_colon")));

                if (!string.Equals(ok, "True", StringComparison.OrdinalIgnoreCase))
                {
                    detail += " error=" + Str(table.Get<object>("error"));
                    return false;
                }

                return BridgeVisibility.IsReachableType(emitExact)
                       && BridgeVisibility.IsReachableType(playerFieldExact);
            }
            catch (Exception e)
            {
                detail = "visibility snippet threw " + Describe(e);
                return false;
            }
        }

        public void SetBridge(ProbeBridge bridge)
        {
            _bridge = bridge;
            if (_env == null)
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

        private bool InstallBridge(ProbeBridge bridge, out string detail)
        {
            try
            {
                _env.Global.Set("probe", bridge);

                string visible;
                if (VerifyBridge(out visible))
                {
                    detail = "_env.Global[\"probe\"] = bridge; " + visible;
                    return true;
                }

                detail = "bridge assignment did not expose members (" + visible + ")";
                return false;
            }
            catch (Exception e)
            {
                detail = "bridge assignment threw " + Describe(e);
                return false;
            }
        }

        public string Diagnostics()
        {
            if (_env == null)
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

        private object[] ConvertArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new object[0];
            }

            var converted = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                converted[i] = args[i] is ProbeContextDto ? BuildContextTable((ProbeContextDto)args[i]) : args[i];
            }

            return converted;
        }

        private LuaTable BuildContextTable(ProbeContextDto dto)
        {
            LuaTable root = _env.NewTable();
            root.Set("schema_version", dto.SchemaVersion);
            root.Set("scenario", dto.Scenario ?? string.Empty);

            LuaTable events = _env.NewTable();
            if (dto.Events != null)
            {
                for (int i = 0; i < dto.Events.Length; i++)
                {
                    events.Set(i + 1, BuildEventTable(dto.Events[i]));
                }
            }

            root.Set("events", events);
            root.Set("player", BuildPlayerTable(dto.Player));
            return root;
        }

        private LuaTable BuildEventTable(ProbeEventDto ev)
        {
            LuaTable t = _env.NewTable();
            t.Set("id", ev.Id);
            t.Set("kind", ev.Kind ?? string.Empty);

            LuaTable tags = _env.NewTable();
            if (ev.Tags != null)
            {
                for (int i = 0; i < ev.Tags.Length; i++)
                {
                    tags.Set(i + 1, ev.Tags[i]);
                }
            }

            tags.Set("threat", ev.Threat);
            t.Set("tags", tags);

            LuaTable actor = _env.NewTable();
            if (ev.Actor != null)
            {
                actor.Set("name", ev.Actor.Name ?? string.Empty);
                actor.Set("hostile", ev.Actor.Hostile);
                actor.Set("level", ev.Actor.Level);
            }

            t.Set("actor", actor);
            return t;
        }

        private LuaTable BuildPlayerTable(ProbePlayerDto p)
        {
            LuaTable t = _env.NewTable();
            if (p == null)
            {
                t.Set("available", false);
                t.Set("reason", "no player snapshot attached");
                return t;
            }

            t.Set("available", p.Available);
            t.Set("reason", p.Reason ?? string.Empty);
            t.Set("name", p.Name ?? string.Empty);
            t.Set("display_name", p.DisplayName ?? string.Empty);
            t.Set("health", p.Health);
            t.Set("max_health", p.MaxHealth);
            t.Set("stamina", p.Stamina);
            t.Set("game_stage", p.GameStage);
            t.Set("is_dead", p.IsDead);
            t.Set("is_admin", p.IsAdmin);
            t.Set("pos_x", p.X);
            t.Set("pos_y", p.Y);
            t.Set("pos_z", p.Z);
            t.Set("world_guid", p.WorldGuid ?? string.Empty);
            t.Set("world_day", p.WorldDay);
            t.Set("world_hour", p.WorldHour);
            t.Set("captured_on_frame", p.CapturedOnFrame);
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
                double d = (double)o;
                return LuaValue.FromNumber(d, MoonSharpBackend.IsIntegral(d));
            }

            if (o is float)
            {
                float f = (float)o;
                return LuaValue.FromNumber(f, MoonSharpBackend.IsIntegral(f));
            }

            string typeName = o.GetType().Name;
            if (typeName == "LuaUserData")
            {
                return LuaValue.FromOther(LuaValueKind.Userdata, o);
            }

            if (o is Enum)
            {
                return LuaValue.FromOther(LuaValueKind.Userdata, o);
            }

            return LuaValue.FromOther(LuaValueKind.Other, o);
        }

        private static string Str(object value)
        {
            if (value == null)
            {
                return "nil";
            }

            string text = value as string;
            return text ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string Summarize(string text)
        {
            if (text == null)
            {
                return "nil";
            }

            return text.Length <= 40 ? text : text.Substring(0, 40) + "...";
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

            return string.Join(" ", parts.ToArray());
        }
    }
#else
    /// <summary>
    /// Stub used when the mod is built without -p:WithXLua=true.  It reports exactly
    /// why it is a stub and what to do instead, so the route is never silently absent.
    /// </summary>
    public sealed class XLuaBackend : ILuaProbeBackend
    {
        private readonly LuaBackendInfo _info = new()
        {
            Id = "xlua",
            DisplayName = "xLua - NOT COMPILED IN (build with -p:WithXLua=true)",
            EmbedVersion = "XLua.Mini " + XLuaAssessment.SourceRef + " (not referenced by this build)",
            DeclaredLuaLanguageVersion = "5.4",
            RuntimeKind = "native (P/Invoke into xlua.dll) + managed reflection layer",
            NativeDependency = XLuaAssessment.NativeLibrary,
            PackageSource = "GitHub Tencent/xLua " + XLuaAssessment.SourceRef + " (no NuGet package)",
            Notes = XLuaAssessment.Summary,
        };

        public LuaBackendInfo Info
        {
            get { return _info; }
        }

        public bool IsAvailable
        {
            get { return false; }
        }

        public string UnavailableReason
        {
            get { return XLuaAssessment.NotCompiledInReason; }
        }

        public bool IsInitialized
        {
            get { return false; }
        }

        public bool Initialize(out string error)
        {
            error = UnavailableReason;
            return false;
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }

        public bool LoadScriptFile(string relativePath, string globalName, out string error)
        {
            error = UnavailableReason;
            return false;
        }

        public bool LoadScriptText(string source, string chunkName, string globalName, out string error)
        {
            error = UnavailableReason;
            return false;
        }

        public LuaValue CallFunction(string moduleGlobal, string functionName, object[] args, out string error)
        {
            error = UnavailableReason;
            return LuaValue.Nil();
        }

        public LuaValue TableGet(LuaValue table, string key, out string error)
        {
            error = UnavailableReason;
            return LuaValue.Nil();
        }

        public LuaValue TableGetInt(LuaValue table, int index, out string error)
        {
            error = UnavailableReason;
            return LuaValue.Nil();
        }

        public bool RequireProbe(string moduleName, out string detail)
        {
            detail = UnavailableReason;
            return false;
        }

        public bool VerifyBridge(out string detail)
        {
            detail = UnavailableReason;
            return false;
        }

        public void InvalidatePlayerSnapshot()
        {
        }

        public void ClearRetryableFailure()
        {
        }

        public void Tick()
        {
        }

        public string Diagnostics()
        {
            return "stub (not compiled in): build with -p:WithXLua=true and stage XLua.Mini.dll + native\\xlua.dll";
        }
    }
#endif
}
