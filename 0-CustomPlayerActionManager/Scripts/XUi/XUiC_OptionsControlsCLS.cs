﻿using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

public class XUiC_OptionsControlsCLS : XUiC_OptionsControls
{
}

[HarmonyPatch]
public static class GetBidningValuePatch
{
#pragma warning disable CS0162
    private static IEnumerable<MethodBase> TargetMethods()
    {
        if (Constants.cVersionMajor <= 2 && Constants.cVersionMinor <= 2)
        {
            Log.Out($"Choosing old GetBindingValue for XUiController for game version {Constants.cVersionMajor}.{Constants.cVersionMinor}");
            yield return AccessTools.Method(typeof(XUiController), "GetBindingValue");
        }
        else
        {
            Log.Out($"Choosing new GetBindingValueInternal for XUiController for game version {Constants.cVersionMajor}.{Constants.cVersionMinor}");
            yield return AccessTools.Method(typeof(XUiController), "GetBindingValueInternal");
        }
    }
#pragma warning restore CS0162

    private static void Postfix(ref string _value, string _bindingName, ref bool __result, XUiController __instance)
    {
        if (__result)
        {
            return;
        }
        if (!string.IsNullOrEmpty(_bindingName) && __instance is XUiC_OptionsControlsCLS cls && _bindingName.StartsWith("keybindingEntryCount"))
        {
            if (CustomPlayerActionManager.arr_row_counts_control == null)
            {
                ReversePatches.InitPlayerActionList(cls);
            }
            int index = int.Parse(_bindingName.Substring(_bindingName.Length - 1));
            _value = CustomPlayerActionManager.arr_row_counts_control[index].ToString();
            __result = true;
        }
    }
}