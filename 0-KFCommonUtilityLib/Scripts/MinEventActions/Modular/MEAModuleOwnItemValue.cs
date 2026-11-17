using HarmonyLib;
using KFCommonUtilityLib.Attributes;

[TypeTarget(typeof(MinEventActionBase))]
public class MEAModuleOwnItemValue
{
    [HarmonyPatch(nameof(MinEventActionBase.CanExecute))]
    [HarmonyPatch(nameof(MinEventActionBase.Execute))]
    [MethodTargetPrefix, HarmonyPriority(Priority.First)]
    public void Prefix_ItemValue(MinEventParams _params, out (bool switched, ItemValue previous) __state)
    {
        var source = ItemEventSourcePatches.EventSource;
        __state = (source != null, _params.ItemValue);
        if (__state.switched)
            _params.ItemValue = source;
    }

    [HarmonyPatch(nameof(MinEventActionBase.CanExecute))]
    [HarmonyPatch(nameof(MinEventActionBase.Execute))]
    [MethodTargetPostfix, HarmonyPriority(Priority.Last)]
    public void Postfix_ItemValue(MinEventParams _params, (bool switched, ItemValue previous) __state)
    {
        if (__state.switched)
            _params.ItemValue = __state.previous;
    }
}
