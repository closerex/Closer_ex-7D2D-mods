namespace LuaRuntimeProbe.Backends
{
    /// <summary>
    /// Facts about the xLua route that both the integrated backend and the
    /// not-compiled-in stub report, so `luaprobe status` cannot drift from the
    /// written assessment (docs/xlua-assessment.md).
    /// </summary>
    public static class XLuaAssessment
    {
        public const string SourceRef = "v2.1.16_ndkr21b_with_win";

        public const string ManagedAssembly = "XLua.Mini.dll (official General build: XLUA_GENERAL, no UnityEngine reference)";

        public const string NativeLibrary = "native/xlua.dll (win-x64, from the matching release bundle)";

        public const string Summary =
            "Integrated when built with -p:WithXLua=true. Managed half is xLua's OWN General "
            + "(XLUA_GENERAL) configuration built from source, so nothing upstream was modified; "
            + "the native half is the matching prebuilt xlua.dll.";

        public const string NotCompiledInReason =
            "this build was produced WITHOUT -p:WithXLua=true, so the xLua backend is a stub. "
            + "Build and stage it with tools/Build-XLuaGeneral.ps1, then build the mod with "
            + "-p:WithXLua=true -p:XLuaManagedPath=<staging>\\XLua.Mini.dll to compile the real backend in. "
            + "Evidence gathered on the General build (offline, .NET Framework 4.8): `new LuaEnv()` succeeds, "
            + "_VERSION reports \"Lua 5.4\", 7 // 2 == 3, math.type(7) == \"integer\", nested table access works, "
            + "and the C# bridge is reachable with EXACT member names and COLON syntax "
            + "(probe:Emit(1) works, probe.Emit(1) throws NullReferenceException inside the emitted wrapper, "
            + "probe.emit is nil) - the same calling convention NLua requires, which is why the portable "
            + "probe_core.lua runs unchanged. The Unity-flavoured build additionally failed offline with "
            + "ArgumentException: Can not find System.MonoType, which is exactly the block removed by the "
            + "upstream #if !XLUA_GENERAL guard; see docs/xlua-assessment.md for that earlier evidence chain.";

        public const string OfflineVerificationNote =
            "Offline evidence for xLua comes from the General build; the in-game payload proposed for "
            + "deployment is the SAME General build plus the same native xlua.dll (sha256 recorded in the "
            + "staging manifest), so the two ends share one managed configuration. The earlier in-game smoke "
            + "run performed by the reviewer used a Unity-flavoured managed build with the identical native "
            + "library; the boundary between those two managed builds is recorded, not blurred.";
    }
}
