using System;
using System.Collections.Generic;
using System.IO;
using LuaRuntimeProbe.Contracts;
using LuaRuntimeProbe.Harness;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
namespace LuaRuntimeProbe.Backends
{
    /// <summary>
    /// MoonSharp: a 100% managed Lua 5.2 implementation, distributed as the
    /// NuGet package `MoonSharp` 2.0.0.  No native code, therefore the only
    /// route that cannot be blocked by the platform's native loading policy.
    /// </summary>
    public sealed class MoonSharpBackend : ILuaProbeBackend, IProbeBridgeHost
    {
        private const string PackageId = "MoonSharp";
        private const string PackageVersion = "2.0.0";

        private readonly LuaBackendInfo _info = new()
        {
            Id = "moonsharp",
            DisplayName = "MoonSharp (pure managed)",
            EmbedVersion = PackageId + " " + PackageVersion,
            DeclaredLuaLanguageVersion = "5.2",
            RuntimeKind = "pure managed (AnyCPU CLR)",
            NativeDependency = "none",
            PackageSource = "nuget.org " + PackageId + " " + PackageVersion
                            + " (asset selected for net48: lib/net40-client/MoonSharp.Interpreter.dll)",
            Notes = "Lua 5.2 language level; no integer subtype; no utf8 library.",
        };

        private Script _script;
        private ProbeBridge _bridge;
        private string _bridgeDetail;

        /// <summary>
        /// Resolved function handles, keyed by "module.function".  Without this
        /// the "hot call" benchmark would silently include a global-table lookup
        /// per iteration, which is not what the row claims to measure.
        /// </summary>
        private readonly Dictionary<string, DynValue> _functionCache = new();

        public LuaBackendInfo Info
        {
            get { return _info; }
        }

        public bool IsAvailable
        {
            get { return MoonSharpBackendProbe.AssemblyResolved; }
        }

        public string UnavailableReason
        {
            get { return MoonSharpBackendProbe.Detail; }
        }

        public bool IsInitialized
        {
            get { return _script != null; }
        }

        public bool Initialize(out string error)
        {
            error = null;

            if (_script != null)
            {
                return true;
            }

            try
            {
                // Registering the bridge type makes its public members reachable
                // from script as a userdata.  RegisterType is idempotent.
                UserData.RegisterType<ProbeBridge>();

                _script = new Script(CoreModules.Preset_Complete);

                // Script print() must never reach the game log per iteration.
                _script.Options.DebugPrint = s => { if (_bridge != null) { _bridge.Log("[print] " + s); } };

                // Resolve `require` relative to <mod>/Lua instead of the game CWD.
                _script.Options.ScriptLoader = new ModFolderScriptLoader(ProbeHost.Current.LuaDirectory);

                _bridge = new ProbeBridge(ProbeHost.Current.CapturePlayer);

                DynValue probeValue = DynValue.FromObject(_script, _bridge);
                if (probeValue == null || probeValue.Type != DataType.UserData)
                {
                    throw new InvalidOperationException(
                        "MoonSharp userdata interop unavailable: DynValue.FromObject returned " +
                        (probeValue == null ? "null" : probeValue.Type.ToString()));
                }

                _script.Globals.Set("probe", probeValue);

                string bridgeDetail;
                if (!VerifyBridge(out bridgeDetail))
                {
                    throw new InvalidOperationException("probe bridge is not reachable from script: " + bridgeDetail);
                }

                _bridgeDetail = bridgeDetail;

                DynValue version = _script.DoString("return _VERSION", null, "=version");
                _info.MeasuredLuaVersion = version != null && version.Type == DataType.String
                    ? version.String
                    : "<not reported>";

                return true;
            }
            catch (Exception e)
            {
                error = Describe(e);
                Shutdown();
                return false;
            }
        }

        public void Shutdown()
        {
            // MoonSharp's Script has no Dispose; dropping the references releases
            // the interpreter graph to the GC on the next collection.
            _script = null;
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
            // MoonSharp has no permanent failure state: a failed Initialize always
            // leaves _script == null, so the next call retries from scratch.
        }

        public void Tick()
        {
            // MoonSharp is fully managed: the CLR GC collects the interpreter graph
            // directly, so there is no deferred native release queue to drain.
        }

        /// <summary>
        /// Swaps the bridge that scripts see as the global `probe` without
        /// touching the interpreter state or the loaded modules.
        /// </summary>
        public void SetBridge(ProbeBridge bridge)
        {
            _bridge = bridge;
            if (_script == null)
            {
                return;
            }

            DynValue probeValue = DynValue.FromObject(_script, bridge);
            if (probeValue == null || probeValue.Type != DataType.UserData)
            {
                throw new InvalidOperationException(
                    "MoonSharp SetBridge: DynValue.FromObject returned " +
                    (probeValue == null ? "null" : probeValue.Type.ToString()));
            }

            _script.Globals.Set("probe", probeValue);

            string detail;
            if (VerifyBridge(out detail))
            {
                _bridgeDetail = detail;
            }
            else
            {
                _bridgeDetail = "SetBridge verification failed: " + detail;
                ProbeHost.Current.LogWarning("[LuaRuntimeProbe] " + _bridgeDetail);
            }
        }

        /// <summary>
        /// Proves that scripts can actually reach the members of `probe` rather
        /// than merely that a global was assigned.
        /// </summary>
        public bool VerifyBridge(out string detail)
        {
            detail = null;

            if (_script == null)
            {
                detail = "backend not initialized";
                return false;
            }

            try
            {
                DynValue value = _script.DoString(BridgeVisibility.Snippet, null, "=bridge_visibility");
                if (value == null || value.Type != DataType.Table)
                {
                    detail = "visibility snippet returned " + (value == null ? "null" : value.Type.ToString());
                    return false;
                }

                Table table = value.Table;
                string probeType = Display(table.Get("probe_type"));
                string emitExact = Display(table.Get("emit_exact"));
                string emitLower = Display(table.Get("emit_lower"));
                string playerFieldExact = Display(table.Get("player_field_exact"));
                string playerFieldLower = Display(table.Get("player_field_lower"));
                string ok = Display(table.Get("ok"));

                detail = "probe=" + probeType
                         + " Emit=" + emitExact + " emit=" + emitLower
                         + " PlayerField=" + playerFieldExact + " player_field=" + playerFieldLower
                         + " | Version()=" + Display(table.Get("version_dot"))
                         + " Version:()=" + Display(table.Get("version_colon"))
                         + " | Emit(1)=" + Display(table.Get("emit_dot"))
                         + " Emit:(1)=" + Display(table.Get("emit_colon"))
                         + " | PlayerField('health')=" + Display(table.Get("player_field_dot"))
                         + " PlayerField:('health')=" + Display(table.Get("player_field_colon"));

                if (ok != "true")
                {
                    detail += " error=" + Display(table.Get("error"));
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

        private static string Display(DynValue value)
        {
            if (value == null)
            {
                return "nil";
            }

            return value.Type == DataType.String ? value.String : value.ToPrintString();
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

            if (_script == null)
            {
                error = "backend not initialized";
                return false;
            }

            try
            {
                DynValue function = _script.LoadString(source, null, chunkName);
                DynValue result = _script.Call(function);
                _script.Globals.Set(globalName, result);

                // A module global was replaced, so any cached handle into it is stale.
                _functionCache.Clear();
                return true;
            }
            catch (InterpreterException e)
            {
                error = DescribeInterpreterException(e);
                return false;
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

            if (_script == null)
            {
                error = "backend not initialized";
                return LuaValue.Nil();
            }

            try
            {
                DynValue function = ResolveFunction(moduleGlobal, functionName, out string lookupError);
                if (function == null)
                {
                    error = lookupError;
                    return LuaValue.Nil();
                }

                DynValue result = _script.Call(function, ConvertArguments(args));
                return FromDynValue(result);
            }
            catch (InterpreterException e)
            {
                error = DescribeInterpreterException(e);
                return LuaValue.Nil();
            }
            catch (Exception e)
            {
                error = Describe(e);
                return LuaValue.Nil();
            }
        }

        private DynValue ResolveFunction(string moduleGlobal, string functionName, out string error)
        {
            error = null;
            string key = moduleGlobal + "." + functionName;

            DynValue cached;
            if (_functionCache.TryGetValue(key, out cached) && cached != null && cached.Type == DataType.Function)
            {
                return cached;
            }

            DynValue module = _script.Globals.Get(moduleGlobal);
            if (module == null || module.Type != DataType.Table)
            {
                error = "global '" + moduleGlobal + "' is not a table (got "
                        + (module == null ? "null" : module.Type.ToString()) + ")";
                return null;
            }

            DynValue function = module.Table.Get(functionName);
            if (function == null || function.Type != DataType.Function)
            {
                error = "global '" + key + "' is not a function (got "
                        + (function == null ? "null" : function.Type.ToString()) + ")";
                return null;
            }

            _functionCache[key] = function;
            return function;
        }

        public LuaValue TableGet(LuaValue table, string key, out string error)
        {
            error = null;

            var t = table != null ? table.Raw as Table : null;
            if (t == null)
            {
                error = "value is not a table (kind=" + (table == null ? "null" : table.KindName) + ")";
                return LuaValue.Nil();
            }

            try
            {
                return FromDynValue(t.Get(key));
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

            var t = table != null ? table.Raw as Table : null;
            if (t == null)
            {
                error = "value is not a table (kind=" + (table == null ? "null" : table.KindName) + ")";
                return LuaValue.Nil();
            }

            try
            {
                return FromDynValue(t.Get(index));
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

            if (_script == null)
            {
                detail = "backend not initialized";
                return false;
            }

            try
            {
                DynValue result = _script.DoString(
                    "return require('" + moduleName + "')", null, "=require_probe");
                detail = "require('" + moduleName + "') returned " + FromDynValue(result).Display
                         + " (loader: ModFolderScriptLoader over " + ProbeHost.Current.LuaDirectory + ")";
                return true;
            }
            catch (InterpreterException e)
            {
                detail = DescribeInterpreterException(e);
                return false;
            }
            catch (Exception e)
            {
                detail = Describe(e);
                return false;
            }
        }

        public string Diagnostics()
        {
            if (_script == null)
            {
                return "not initialised";
            }

            return "state alive; _VERSION=" + (_info.MeasuredLuaVersion ?? "?")
                   + "; globals=probe" + (_bridge != null ? "(userdata)" : "(missing)")
                   + "; bridge: " + (_bridgeDetail ?? "<not installed>");
        }

        // ------------------------------------------------------------------
        // Marshalling
        // ------------------------------------------------------------------

        private Table NewTable()
        {
            // DynValue.NewTable(script) is the public way to allocate a fresh table.
            return DynValue.NewTable(_script).Table;
        }

        private DynValue[] ConvertArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new DynValue[0];
            }

            var converted = new DynValue[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                converted[i] = ConvertArgument(args[i]);
            }

            return converted;
        }

        private DynValue ConvertArgument(object arg)
        {
            if (arg == null)
            {
                return DynValue.NewNil();
            }

            if (arg is bool)
            {
                return DynValue.NewBoolean((bool)arg);
            }

            if (arg is string)
            {
                return DynValue.NewString((string)arg);
            }

            if (arg is int)
            {
                return DynValue.NewNumber((int)arg);
            }

            if (arg is long)
            {
                return DynValue.NewNumber((long)arg);
            }

            if (arg is short)
            {
                return DynValue.NewNumber((short)arg);
            }

            if (arg is byte)
            {
                return DynValue.NewNumber((byte)arg);
            }

            if (arg is float)
            {
                return DynValue.NewNumber((float)arg);
            }

            if (arg is double)
            {
                return DynValue.NewNumber((double)arg);
            }

            if (arg is ProbeContextDto)
            {
                return DynValue.NewTable(BuildContextTable((ProbeContextDto)arg));
            }

            throw new ScriptRuntimeException(
                "MoonSharpBackend.ConvertArgument: unsupported argument type " + arg.GetType().FullName);
        }

        private Table BuildContextTable(ProbeContextDto dto)
        {
            Table root = NewTable();
            root.Set("schema_version", DynValue.NewNumber(dto.SchemaVersion));
            root.Set("scenario", DynValue.NewString(dto.Scenario ?? string.Empty));

            Table events = NewTable();
            if (dto.Events != null)
            {
                for (int i = 0; i < dto.Events.Length; i++)
                {
                    events.Set(i + 1, DynValue.NewTable(BuildEventTable(dto.Events[i])));
                }
            }

            root.Set("events", DynValue.NewTable(events));
            root.Set("player", DynValue.NewTable(BuildPlayerTable(dto.Player)));
            return root;
        }

        private Table BuildEventTable(ProbeEventDto ev)
        {
            Table t = NewTable();
            t.Set("id", DynValue.NewNumber(ev.Id));
            t.Set("kind", DynValue.NewString(ev.Kind ?? string.Empty));

            Table tags = NewTable();
            if (ev.Tags != null)
            {
                for (int i = 0; i < ev.Tags.Length; i++)
                {
                    tags.Set(i + 1, DynValue.NewString(ev.Tags[i]));
                }
            }

            tags.Set("threat", DynValue.NewNumber(ev.Threat));
            t.Set("tags", DynValue.NewTable(tags));

            Table actor = NewTable();
            if (ev.Actor != null)
            {
                actor.Set("name", DynValue.NewString(ev.Actor.Name ?? string.Empty));
                actor.Set("hostile", DynValue.NewBoolean(ev.Actor.Hostile));
                actor.Set("level", DynValue.NewNumber(ev.Actor.Level));
            }

            t.Set("actor", DynValue.NewTable(actor));
            return t;
        }

        private Table BuildPlayerTable(ProbePlayerDto p)
        {
            Table t = NewTable();
            if (p == null)
            {
                t.Set("available", DynValue.NewBoolean(false));
                t.Set("reason", DynValue.NewString("no player snapshot attached"));
                return t;
            }

            t.Set("available", DynValue.NewBoolean(p.Available));
            t.Set("reason", DynValue.NewString(p.Reason ?? string.Empty));
            t.Set("name", DynValue.NewString(p.Name ?? string.Empty));
            t.Set("display_name", DynValue.NewString(p.DisplayName ?? string.Empty));
            t.Set("health", DynValue.NewNumber(p.Health));
            t.Set("max_health", DynValue.NewNumber(p.MaxHealth));
            t.Set("stamina", DynValue.NewNumber(p.Stamina));
            t.Set("game_stage", DynValue.NewNumber(p.GameStage));
            t.Set("is_dead", DynValue.NewBoolean(p.IsDead));
            t.Set("is_admin", DynValue.NewBoolean(p.IsAdmin));
            t.Set("pos_x", DynValue.NewNumber(p.X));
            t.Set("pos_y", DynValue.NewNumber(p.Y));
            t.Set("pos_z", DynValue.NewNumber(p.Z));
            t.Set("world_guid", DynValue.NewString(p.WorldGuid ?? string.Empty));
            t.Set("world_day", DynValue.NewNumber(p.WorldDay));
            t.Set("world_hour", DynValue.NewNumber(p.WorldHour));
            t.Set("captured_on_frame", DynValue.NewNumber(p.CapturedOnFrame));
            return t;
        }

        private static LuaValue FromDynValue(DynValue v)
        {
            if (v == null)
            {
                return LuaValue.Nil();
            }

            switch (v.Type)
            {
                case DataType.Nil:
                case DataType.Void:
                    return LuaValue.Nil();
                case DataType.Boolean:
                    return LuaValue.FromBool(v.Boolean);
                case DataType.Number:
                    return LuaValue.FromNumber(v.Number, IsIntegral(v.Number));
                case DataType.String:
                    return LuaValue.FromString(v.String);
                case DataType.Table:
                    return LuaValue.FromOther(LuaValueKind.Table, v.Table);
                case DataType.Function:
                    return LuaValue.FromOther(LuaValueKind.Function, v.Function);
                case DataType.UserData:
                    return LuaValue.FromOther(LuaValueKind.Userdata, v.UserData);
                case DataType.Thread:
                    return LuaValue.FromOther(LuaValueKind.Thread, v.Coroutine);
                default:
                    return LuaValue.FromOther(LuaValueKind.Other, v);
            }
        }

        internal static bool IsIntegral(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                return false;
            }

            return Math.Floor(d) == d;
        }

        private static string DescribeInterpreterException(InterpreterException e)
        {
            string message = null;
            try
            {
                message = e.DecoratedMessage;
            }
            catch (Exception)
            {
                // DecoratedMessage can throw if the message is not decidable.
            }

            if (string.IsNullOrEmpty(message))
            {
                message = e.Message;
            }

            return e.GetType().Name + ": " + message;
        }

        private static string Describe(Exception e)
        {
            string inner = e.InnerException != null
                ? " <- " + e.InnerException.GetType().Name + ": " + e.InnerException.Message
                : string.Empty;
            return e.GetType().Name + ": " + e.Message + inner;
        }
    }

    /// <summary>
    /// MoonSharp lives in a separate assembly.  Resolving it by NAME (instead of
    /// touching the type directly) means a missing assembly is reported as a
    /// finding instead of a JIT-time TypeLoadException.
    /// </summary>
    internal static class MoonSharpBackendProbe
    {
        public static readonly bool AssemblyResolved;
        public static readonly string Detail;

        static MoonSharpBackendProbe()
        {
            try
            {
                Type t = Type.GetType("MoonSharp.Interpreter.Script, MoonSharp.Interpreter", false);
                if (t == null)
                {
                    AssemblyResolved = false;
                    Detail = "assembly MoonSharp.Interpreter could not be resolved by name";
                }
                else
                {
                    AssemblyResolved = true;
                    Detail = "resolved " + t.Assembly.GetName().Name + " " + t.Assembly.GetName().Version
                             + " from " + SafeLocation(t.Assembly);
                }
            }
            catch (Exception e)
            {
                AssemblyResolved = false;
                Detail = "probe threw " + e.GetType().Name + ": " + e.Message;
            }
        }

        private static string SafeLocation(System.Reflection.Assembly a)
        {
            try
            {
                return a.Location;
            }
            catch (Exception)
            {
                return "<no location>";
            }
        }
    }

    /// <summary>
    /// Resolves `require`/`loadfile` module names inside &lt;mod&gt;/Lua so that
    /// script module reuse works the same way it would in a mod.
    /// </summary>
    internal sealed class ModFolderScriptLoader : IScriptLoader
    {
        private readonly string _root;

        public ModFolderScriptLoader(string root)
        {
            _root = root;
        }

        public object LoadFile(string file, Table globalContext)
        {
            string relative = (file ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return File.ReadAllText(Path.Combine(_root, relative));
        }

        public string ResolveFileName(string filename, Table globalContext)
        {
            return filename;
        }

        public string ResolveModuleName(string modname, Table globalContext)
        {
            return (modname ?? string.Empty).Replace('.', '/') + ".lua";
        }
    }
}
