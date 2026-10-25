using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

[UsedImplicitly]
public sealed class XUiC_OptionsControlsCLS : XUiC_OptionsControls
{
}

// ReSharper disable PossibleNullReferenceException
[HarmonyPatch]
public static class GetBidningValuePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Log.Out($"Choosing new GetBindingValueInternal for XUiController for game version {Constants.cVersionInformation.Major}.{Constants.cVersionInformation.Minor}");
        yield return AccessTools.Method(typeof(XUiController), "GetBindingValueInternal");
    }

    private static void Postfix(ref string _value, string _bindingName, ref bool __result, XUiController __instance)
    {
        if (__result)
        {
            return;
        }
        if (!string.IsNullOrEmpty(_bindingName) && __instance is XUiC_OptionsControlsCLS or XUiC_OptionsController && _bindingName.StartsWith("keybindingEntryCount"))
        {
            if (__instance is XUiC_OptionsControlsCLS cls)
            {
                if (CustomPlayerActionManager.arr_row_counts_control == null)
                {
                    ReversePatches.InitPlayerActionList(cls);
                }
                int index = int.Parse(_bindingName[^1..]);
                _value = CustomPlayerActionManager.arr_row_counts_control[index].ToString();
                __result = true;
            }
            else if (__instance is XUiC_OptionsController controller)
            {
                if (CustomPlayerActionManager.arr_row_counts_controller == null)
                {
                    ReversePatches.InitControllerActionList(controller);
                }
                int index = int.Parse(_bindingName[^1..]);
                _value = CustomPlayerActionManager.arr_row_counts_controller[index].ToString();
                __result = true;
            }
        }
    }
}