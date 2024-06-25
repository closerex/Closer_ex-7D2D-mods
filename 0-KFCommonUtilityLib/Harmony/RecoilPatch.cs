using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

[HarmonyPatch]
class RecoilPatch
{
    [HarmonyPatch(typeof(EntityPlayerLocal), "Awake")]
    [HarmonyPostfix]
    private static void Postfix_Awake_EntityPlayerLocal(EntityPlayerLocal __instance)
    {
        RecoilManager.InitPlayer(__instance);
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
    [HarmonyPostfix]
    private static void Postfix_SaveAndCleanupWorld_GameManager()
    {
        RecoilManager.Cleanup();
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnFired))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_OnFired_EntityPlayerLocal(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        FieldInfo fld_isfpv = AccessTools.Field(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.bFirstPersonView));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_isfpv))
            {
                codes.RemoveRange(i + 2, codes.Count - i - 3);
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    CodeInstruction.Call(typeof(RecoilManager), nameof(RecoilManager.AddRecoil))
                });
                break;
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMoveController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        FieldInfo fld_rotation = AccessTools.Field(typeof(MovementInput), nameof(MovementInput.rotation));
        FieldInfo fld_rx = AccessTools.Field(typeof(Vector3), nameof(Vector3.x));
        FieldInfo fld_ry = AccessTools.Field(typeof(Vector3), nameof(Vector3.y));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_rotation))
            {
                for (; i < codes.Count; i++)
                {
                    if (codes[i].StoresField(fld_rx))
                    {
                        codes.Insert(i, CodeInstruction.Call(typeof(RecoilManager), nameof(RecoilManager.CompensateX)));
                        break;
                    }
                    else if (codes[i].StoresField(fld_ry))
                    {
                        codes.Insert(i, CodeInstruction.Call(typeof(RecoilManager), nameof(RecoilManager.CompensateY)));
                        break;
                    }
                }
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.MoveByInput))]
    [HarmonyPrefix]
    private static bool Prefix_MoveByInput_EntityPlayerLocal()
    {
        RecoilManager.ApplyRecoil();
        return true;
    }
}