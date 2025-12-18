using HarmonyLib;
using KFCommonUtilityLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using CameraShake;
using System;

[HarmonyPatch]
class RecoilPatch
{
    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Awake))]
    [HarmonyPostfix]
    private static void Postfix_Awake_EntityPlayerLocal(EntityPlayerLocal __instance)
    {
        RecoilManager.InitPlayer(__instance);
    }

    //[HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.LateUpdate))]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_LateUpdate_vp_FPCamera(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = instructions.ToList();

    //    var mtd_update = AccessTools.Method(typeof(vp_FPCamera), nameof(vp_FPCamera.Update3rdPerson));
    //    var prop_trans = AccessTools.PropertyGetter(typeof(vp_Component), nameof(vp_Component.transform));

    //    for (int i = 0; i < codes.Count; i++)
    //    {
    //        if (codes[i].Calls(mtd_update))
    //        {
    //            codes.InsertRange(i - 1, new[]
    //            {
    //                new CodeInstruction(OpCodes.Ldarg_0),
    //                new CodeInstruction(OpCodes.Call, prop_trans),
    //                CodeInstruction.Call(typeof(KFExtensions), nameof(KFExtensions.AddMissingComponent), new Type[]{ typeof(Transform) }, new Type[]{ typeof(CameraShaker) }),
    //                CodeInstruction.Call(typeof(CameraShaker), nameof(CameraShaker.UpdateShake))
    //            });
    //            break;
    //        }
    //    }

    //    return codes;
    //}

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
        MethodInfo prop_isaiming = AccessTools.PropertyGetter(typeof(EntityAlive), nameof(EntityAlive.AimingGun));
        ConstructorInfo mtd_ctor = AccessTools.Constructor(typeof(Vector2), new[] { typeof(float), typeof(float) });

        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].Is(OpCodes.Call, mtd_ctor) && codes[i + 1].opcode == OpCodes.Ldloc_0)
            {
                for (int j = i + 1; j < codes.Count - 1; j++)
                {
                    if (codes[j].opcode == OpCodes.Pop && codes[j + 1].opcode == OpCodes.Ldarg_0)
                    {
                        codes.RemoveRange(i + 1, j - i);
                        break;
                    }
                }
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldloca_S, 0),
                    new CodeInstruction(OpCodes.Ldloca_S, 1),
                    CodeInstruction.Call(typeof(RecoilPatch), nameof(RecoilPatch.ModifyRecoil))
                });
                i += 4;
            }
            else if (codes[i].Calls(prop_isaiming))
            {
                var lbls = codes[i - 1].ExtractLabels();
                codes.RemoveRange(i - 1, codes.Count - i);
                codes.InsertRange(codes.Count - 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_0).WithLabels(lbls),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    CodeInstruction.Call(typeof(RecoilManager), nameof(RecoilManager.AddRecoil))
                });
                break;
            }
        }
        return codes;
    }

    private static void ModifyRecoil(EntityPlayerLocal player, ref Vector2 kickHor, ref Vector2 kickVer)
    {
        float multiplierHor = Mathf.Max(1 - EffectManager.GetValue(CustomEnums.KickDegreeHorizontalModifier, player.inventory.holdingItemItemValue, 0f, player), 0);
        float multiplierVer = Mathf.Max(1 - EffectManager.GetValue(CustomEnums.KickDegreeVerticalModifier, player.inventory.holdingItemItemValue, 0f, player), 0);
        kickHor *= multiplierHor;
        kickVer *= multiplierVer;
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
            if (codes[i].LoadsField(fld_rotation, true))
            {
                if (codes[i + 1].LoadsField(fld_rx, true))
                {
                    for (; i < codes.Count; i++)
                    {
                        if (codes[i].opcode == OpCodes.Stind_R4)
                        {
                            codes.Insert(i, CodeInstruction.Call(typeof(RecoilManager), nameof(RecoilManager.CompensateX)));
                            break;
                        }
                    }
                }
                else if (codes[i + 1].LoadsField(fld_ry, true))
                {
                    for (; i < codes.Count; i++)
                    {
                        if (codes[i].opcode == OpCodes.Stind_R4)
                        {
                            codes.Insert(i, CodeInstruction.Call(typeof(RecoilManager), nameof(RecoilManager.CompensateY)));
                            break;
                        }
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