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
            __instance.RefreshValue(true);
        }
    }

    [HarmonyPatch]
    public static class GetBindingValuePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            if (Constants.cVersionInformation.CompareTo(new VersionInformation(VersionInformation.EGameReleaseType.V, 2, 3, 0)) < 0)
            {
                yield return AccessTools.Method(typeof(XUiC_OptionsController), "GetBindingValue");
            }
            else
            {
                yield return AccessTools.Method(typeof(XUiC_OptionsController), "GetBindingValueInternal");
            }
        }

        private static void Postfix(ref string _value, string _bindingName, ref bool __result, XUiC_OptionsController __instance)
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
            if (codes[i].opcode == OpCodes.Stloc_0)
            {
                codes.InsertRange(i - 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(XUiC_OptionsController), nameof(XUiC_OptionsController.actionTabGroups)),
                    CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateControllerActions))
                });
                break;
            }
        }

        return codes;
    }


    [HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.SaveControls))]
    [HarmonyPostfix]
    private static void Postfix_SaveControls_GameOptionsManager()
    {
        CustomPlayerActionManager.SaveCustomControls();
    }

    [HarmonyPatch(typeof(XUiC_OptionsControls), nameof(XUiC_OptionsControls.storeCurrentBindings))]
    [HarmonyPostfix]
    private static void Postfix_storeCurrentBindings_XUiC_OptionsControls()
    {
        CustomPlayerActionManager.CacheCustomControls();
    }

    [HarmonyPatch(typeof(XUiC_OptionsController), nameof(XUiC_OptionsController.storeCurrentBindings))]
    [HarmonyPostfix]
    private static void Postfix_storeCurrentBindings_XUiC_OptionsController()
    {
        CustomPlayerActionManager.CacheCustomControls();
    }

    [HarmonyPatch(typeof(XUiC_OptionsControls), nameof(XUiC_OptionsControls.OnClose))]
    [HarmonyPostfix]
    private static void Postfix_OnClose_XUiC_OptionsControls(bool ___closedForNewBinding)
    {
        if (!___closedForNewBinding)
            CustomPlayerActionManager.RestoreCustomControls();
    }

    [HarmonyPatch(typeof(XUiC_OptionsController), nameof(XUiC_OptionsController.OnClose))]
    [HarmonyPostfix]
    private static void Postfix_OnClose_XUiC_OptionsController(bool ___closedForNewBinding)
    {
        if (!___closedForNewBinding)
            CustomPlayerActionManager.RestoreCustomControls();
    }

    [HarmonyPatch(typeof(XUiC_OptionsControls), nameof(XUiC_OptionsControls.BtnDefaults_OnOnPressed))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_BtnDefaults_OnOnPressed_XUiC_OptionsControls(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var prop_actionset = AccessTools.PropertyGetter(typeof(PlayerInputManager), nameof(PlayerInputManager.ActionSets));

        for (int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].Calls(prop_actionset))
            {
                codes[i + 1].operand = AccessTools.Method(typeof(IEnumerable<PlayerActionsBase>), nameof(IEnumerable<PlayerActionsBase>.GetEnumerator));
                codes.InsertRange(i + 1, new[]
                {
                    CodeInstruction.Call(typeof(Enumerable), nameof(Enumerable.ToArray), null, new[] { typeof(PlayerActionsBase) }),
                    CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateActionArray)),
                    CodeInstruction.Call(typeof(Enumerable), nameof(Enumerable.ToList), null, new[] { typeof(PlayerActionsBase) })
                });
                i += 3;
            }
        }
        return codes;
    }

    //[HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.ResetGameOptions))]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_ResetGameOptions_GameOptionsManager(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = new List<CodeInstruction>(instructions);
    //    var mtd_save_controls = AccessTools.Method(typeof(GameOptionsManager), nameof(GameOptionsManager.SaveControls));

    //    for (int i = 0; i < codes.Count; ++i)
    //    {
    //        if (codes[i].Calls(mtd_save_controls))
    //        {
    //            codes.Insert(i + 1, CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.ResetCustomControls)));
    //            i++;
    //        }
    //    }

    //    return codes;
    //}

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

