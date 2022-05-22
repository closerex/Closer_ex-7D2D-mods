using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

[HarmonyPatch]
public class Patches
{
    [HarmonyPatch(typeof(XUiC_OptionsControls), "createControlsEntries")]
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


    [HarmonyPatch(typeof(XUiC_OptionsControls), "storeCurrentBindings")]
    [HarmonyPostfix]
    private static void Postfix_storeCurrentBindings_XUiC_OptionsControls(List<string> ___actionBindingsOnOpen)
    {
        CustomPlayerActionManager.StoreCurrentCustomBindings(___actionBindingsOnOpen);
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
            if (codes[i].opcode == OpCodes.Call && codes[i].Calls(mtd_save_controls))
            {
                var insert = CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.ResetCustomControls));
                var temp = codes[i].operand;
                codes[i].operand = insert.operand;
                insert.operand = temp;
                codes.Insert(++i, insert);
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

