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

    [HarmonyPatch(typeof(XUiC_MainMenu), nameof(XUiC_MainMenu.OnOpen))]
    [HarmonyPrefix]
    private static bool Prefix_OnOpen_XUiC_MainMenu(XUiC_MainMenu __instance)
    {
        __instance.xui.playerUI.windowManager.Close(RandomBackgroundLoader.Cur_name);
        __instance.xui.playerUI.windowManager.Close(RandomBackgroundLoader.Cur_logo);

        return true;
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
                codes.Insert(i + 1, CodeInstruction.Call(typeof(RandomBackgroundLoader), nameof(RandomBackgroundLoader.modWindowName)));
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(XUiC_MainMenu), nameof(XUiC_MainMenu.OnClose))]
    [HarmonyPostfix]
    private static void Postfix_OnClose_XUiC_MainMenu(XUiC_MainMenu __instance)
    {
        __instance.xui.playerUI.windowManager.Close(RandomBackgroundLoader.Cur_logo);
    }
    
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
    [HarmonyPrefix]
    private static bool Prefix_StartGame_GameManager(GameManager __instance)
    {
        if (!GameManager.IsDedicatedServer)
            __instance.windowManager.Close(RandomBackgroundLoader.Cur_name);
        return true;
    }
}
