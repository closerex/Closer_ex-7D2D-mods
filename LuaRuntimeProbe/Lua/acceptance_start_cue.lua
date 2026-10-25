-- Acceptance helper: start the already-loaded CUE MCP adapter once.
-- Called explicitly through luaprobe; does not modify gameplay or focus a window.
return {
    main = function()
        assert(luanet and luanet.import_type, "NLua CLR import is unavailable")
        luanet.load_assembly("CueMcp.RuntimeAdapter")
        local bootstrap = luanet.import_type("CueMcp.RuntimeAdapter.RuntimeAdapterBootstrap")
        assert(bootstrap, "CUE RuntimeAdapterBootstrap type is unavailable")
        local started = bootstrap.Start(nil)
        return "CUE_ACCEPTANCE_BOOTSTRAP started=" .. tostring(started)
    end
}
