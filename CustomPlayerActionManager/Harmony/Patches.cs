using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

[HarmonyPatch]
public class Patches
{
    [HarmonyPatch(typeof(BindingInfo), MethodType.Constructor, new[] { typeof(XUiView), typeof(string), typeof(string) })]
    [HarmonyPostfix]
    private static void Postfix_ctor_BindingInfo(BindingInfo __instance, string _sourceText)
    {
        if (!string.IsNullOrEmpty(_sourceText) && _sourceText.StartsWith("{keybindingEntryCount"))
        {
            __instance.RefreshValue(true);
        }
    }

    [HarmonyPatch(typeof(XUiC_OptionsController), nameof(XUiC_OptionsController.GetBindingValue))]
    [HarmonyPostfix]
    private static void Postfix_GetBindingValue_XUiC_OptionsController(ref string _value, string _bindingName, ref bool __result, XUiC_OptionsController __instance)
    {
        if (__result)
        {
            return;
        }
        if (!string.IsNullOrEmpty(_bindingName) && _bindingName.StartsWith("keybindingEntryCount"))
        {
            if (CustomPlayerActionManager.arr_row_counts_controller == null)
            {
                ReversePatches.InitControllerActionList(__instance);
            }
            int index = int.Parse(_bindingName.Substring(_bindingName.Length - 1));
            _value = CustomPlayerActionManager.arr_row_counts_controller[index].ToString();
            __result = true;
            return;
        }
    }

    [HarmonyPatch(typeof(XUiC_OptionsControls), nameof(XUiC_OptionsControls.createControlsEntries))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_createControlsEntries_XUiC_OptionsControls(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for(int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].opcode == OpCodes.Stloc_2)
            {
                codes.Insert(i, CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateActionArray)));
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(XUiC_OptionsController), nameof(XUiC_OptionsController.createControlsEntries))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_createControlsEntries_XUiC_OptionsController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for(int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].opcode == OpCodes.Stloc_1)
            {
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Dup),
                    CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateControllerActions)),
                });
                break;
            }
        }

        return codes;
    }


    [HarmonyPatch(typeof(XUiC_OptionsControls), nameof(XUiC_OptionsControls.storeCurrentBindings))]
    [HarmonyPostfix]
    private static void Postfix_storeCurrentBindings_XUiC_OptionsControls(XUiC_OptionsControls __instance)
    {
        CustomPlayerActionManager.StoreCurrentCustomBindings(__instance.actionBindingsOnOpen);
    }


    [HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.SaveControls))]
    [HarmonyPostfix]
    private static void Postfix_SaveControls_GameOptionsManager()
    {
        CustomPlayerActionManager.SaveCustomControls();
    }

    [HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.ResetGameOptions))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ResetGameOptions_GameOptionsManager(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var mtd_save_controls = AccessTools.Method(typeof(GameOptionsManager), nameof(GameOptionsManager.SaveControls));

        for (int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].Calls(mtd_save_controls))
            {
                codes.Insert(i + 1, CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.ResetCustomControls)));
                i++;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ActionSetManager), nameof(ActionSetManager.LogActionSets))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_LogActionSets_ActionSetManager(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for(int i = codes.Count - 1; i >= 0; --i)
        {
            if (codes[i].opcode == OpCodes.Ldloc_1)
            {
                codes.Insert(i + 1, CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateDebugInfo)));
                break;
            }
        }

        return codes;
    }
}

