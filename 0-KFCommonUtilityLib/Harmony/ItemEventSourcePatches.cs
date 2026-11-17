using HarmonyLib;

[HarmonyPatch(typeof(ItemValue), nameof(ItemValue.FireEvent))]
public static class ItemEventSourcePatches
{
    // Synchronous event scope; nested events restore the caller's source on normal return.
    public static ItemValue EventSource;

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix_FireEvent(ItemValue __instance, out ItemValue __state)
    {
        __state = EventSource;
        EventSource = __instance.ItemClass is ItemClassModifier ? __instance : null;
    }

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix_FireEvent(ItemValue __state)
    {
        EventSource = __state;
    }
}
