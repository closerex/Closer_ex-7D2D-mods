using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using InControl;

[HarmonyPatch]
internal static class ReversePatches
{
    [HarmonyPatch(typeof(XUiC_OptionsControls), nameof(XUiC_OptionsControls.createControlsEntries))]
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
    internal static void InitPlayerActionList(XUiC_OptionsControls __instance)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (instructions == null)
            {
                yield break;
            }

            var codes = new List<CodeInstruction>(instructions);
            var createActionArray = AccessTools.Method(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateActionArray));
            bool hasCreateActionArray = codes.Exists(code => code.Calls(createActionArray));
            foreach (var code in codes)
            {
                if (!hasCreateActionArray && code.opcode == OpCodes.Stloc_2)
                {
                    yield return CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateActionArray));
                }
                if (code.opcode == OpCodes.Stloc_1)
                {
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.ResizeGrid));
                    yield return new CodeInstruction(OpCodes.Ret);
                    break;
                }

                yield return code;
            }
        }
        _ = Transpiler(null);
    }

    [HarmonyPatch(typeof(XUiC_OptionsController), nameof(XUiC_OptionsController.createControlsEntries))]
    [HarmonyReversePatch]
    internal static void InitControllerActionList(XUiC_OptionsController __instance)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (instructions == null)
            {
                yield break;
            }

            var codes = new List<CodeInstruction>(instructions);
            var createControllerActions = AccessTools.Method(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateControllerActions));
            bool hasCreateControllerActions = codes.Exists(code => code.Calls(createControllerActions));
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Stloc_1)
                {
                    yield return new CodeInstruction(OpCodes.Pop);
                    if (!hasCreateControllerActions)
                    {
                        yield return new CodeInstruction(OpCodes.Ldloc_0);
                        yield return CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.CreateControllerActions));
                    }
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.ResizeControllerGrid));
                    yield return new CodeInstruction(OpCodes.Ret);
                    break;
                }

                yield return code;
            }
        }
        _ = Transpiler(null);
    }
}