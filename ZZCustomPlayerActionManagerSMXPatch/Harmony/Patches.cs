using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

[HarmonyPatch]
public class Patches
{
    [HarmonyPatch(typeof(SMXcore.XUiC_OptionsControls), "createControlsEntries")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_createControlsEntries_XUiC_OptionsControls(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].opcode == OpCodes.Stloc_1)
            {
                codes.Insert(i, CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateActionArray)));
                break;
            }
        }

        return codes;
    }


    [HarmonyPatch(typeof(SMXcore.XUiC_OptionsControls), "storeCurrentBindings")]
    [HarmonyPostfix]
    private static void Postfix_storeCurrentBindings_XUiC_OptionsControls(List<string> ___actionBindingsOnOpen)
    {
        CustomPlayerActionManager.StoreCurrentCustomBindings(___actionBindingsOnOpen);
    }
}

