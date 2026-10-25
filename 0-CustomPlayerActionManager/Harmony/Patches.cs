using GUI_2;
using HarmonyLib;
using Platform;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;

[HarmonyPatch]
public class Patches
{
    [HarmonyPatch(typeof(BindingInfo), MethodType.Constructor, new[] { typeof(XUiView), typeof(string), typeof(string) })]
    [HarmonyPostfix]
    private static void Postfix_ctor_BindingInfo(BindingInfo __instance, string _sourceText)
    {
        if (!string.IsNullOrEmpty(_sourceText) && _sourceText.StartsWith("{keybindingEntryCount"))
        {
            __instance.RefreshValue();
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
                codes.InsertRange(i - 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateControllerActions))
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(GameOptionsControls), nameof(GameOptionsControls.Save))]
    [HarmonyPostfix]
    private static void Postfix_SaveControls_GameOptionsControls()
    {
        CustomPlayerActionManager.SaveCustomControls();
    }

    [HarmonyPatch(typeof(XUiC_OptionsControlsBase), nameof(XUiC_OptionsControlsBase.storeCurrentBindings))]
    [HarmonyPostfix]
    private static void Postfix_storeCurrentBindings_XUiC_OptionsControlsBase()
    {
        CustomPlayerActionManager.CacheCustomControls();
    }

    [HarmonyPatch(typeof(PlayerInputManager), nameof(PlayerInputManager.LoadActionSetsFromStrings))]
    [HarmonyPostfix]
    private static void Postfix_LoadActionSetsFromStrings_PlayerInputManager()
    {
        CustomPlayerActionManager.RestoreCustomControls();
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

    [HarmonyPatch(typeof(PlayerInputManager), nameof(PlayerInputManager.GetActionSetForName))]
    [HarmonyPostfix]
    private static void Postfix_GetActionSetForName(ref PlayerActionsBase __result, string _name)
    {
        if (__result == null)
        {
            if (CustomPlayerActionManager.TryGetCustomActionSetByName(_name, out var actionSet, false))
            {
                __result = actionSet;
            }
        }
    }

    //disable "No device binding source" warning
    [HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetButtonIconForAction))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_GetButtonIconForAction_UIUtils(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldstr && codes[i - 1].Branches(out _))
            {
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldc_I4_S, (int)UIUtils.ButtonIcon.None),
                    new CodeInstruction(OpCodes.Ret)
                });
                break;
            }
        }

        return codes;
    }
}

