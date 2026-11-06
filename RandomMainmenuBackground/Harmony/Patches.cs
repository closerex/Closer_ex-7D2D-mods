using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

[HarmonyPatch]
class Patches
{
    [HarmonyPatch(typeof(XUiFromXml), nameof(XUiFromXml.parseWindowGroup))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_parseWindowGroup_XUiFromXml(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].opcode == OpCodes.Newobj
                && codes[i].operand is ConstructorInfo constructor
                && constructor.DeclaringType == typeof(XUiWindowGroup)
                && codes[i + 1].opcode == OpCodes.Stind_Ref)
            {
                codes.InsertRange(i + 2, new[]
                {
                    CodeInstruction.LoadLocal(0),
                    CodeInstruction.Call(typeof(RandomBackgroundLoader), nameof(RandomBackgroundLoader.insert), new[] { typeof(string) })
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

        for (int i = 0; i < codes.Count; i++)
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

        for (int i = 0; i < codes.Count; i++)
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
