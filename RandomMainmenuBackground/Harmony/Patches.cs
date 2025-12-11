using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

[HarmonyPatch]
class Patches
{
    private static MethodInfo mtdinfo_fwgbn = AccessTools.Method(typeof(XUi), nameof(XUi.FindWindowGroupByName), new Type[] { typeof(string) });

    [HarmonyPatch(typeof(XUiFromXml), nameof(XUiFromXml.LoadXui))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_LoadXui_XUiFromXml(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0, totali = codes.Count; i < totali; i++)
        {
            CodeInstruction code = codes[i];
            if (code.Calls(mtdinfo_fwgbn))
            {
                codes.InsertRange(i + 6, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, 5),
                    CodeInstruction.Call(typeof(RandomBackgroundLoader), nameof(RandomBackgroundLoader.insert), new Type[] { typeof(string) })
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(XUiC_MainMenu), nameof(XUiC_MainMenu.OpenGlobalMenuWindows))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_OpenGlobalMenuWindows_XUiC_MainMenu(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0, totali = codes.Count; i < totali; i++)
        {
            CodeInstruction code = codes[i];
            if (code.opcode == OpCodes.Ldloc_2)
            {
                codes.InsertRange(i + 1, new[]
                {
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.Call(typeof(RandomBackgroundLoader), nameof(RandomBackgroundLoader.modWindowName))
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(XUiC_MainMenu), nameof(XUiC_MainMenu.CloseGlobalMenuWindows))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_CloseGlobalMenuWindows_XUiC_MainMenu(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0, totali = codes.Count; i < totali; i++)
        {
            CodeInstruction code = codes[i];
            if (code.opcode == OpCodes.Ldloc_2)
            {
                codes.InsertRange(i + 1, new[]
                {
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.Call(typeof(RandomBackgroundLoader), nameof(RandomBackgroundLoader.modWindowName))
                });
                break;
            }
        }

        return codes;
    }
}
