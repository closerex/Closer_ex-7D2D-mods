using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using KFCommonUtilityLib.Scripts.Utilities;
using UniLinq;
using UnityEngine;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class ProjectilePatches
    {
        [HarmonyPatch(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.DoRevive))]
        [HarmonyPostfix]
        private static void Postfix_DoRevive_ProjectileMoveScript(ProjectileMoveScript __instance)
        {
            if (__instance is CustomProjectileMoveScript && __instance.state == ProjectileMoveScript.State.Active)
            {
                __instance.velocity = Vector3.zero;
            }
        }
        
        [HarmonyPatch(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.FixedUpdate))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_FixedUpdate_ProjectileMoveScript(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            
            var fld_flytime = AccessTools.Field(typeof(ItemActionProjectile), nameof(ItemActionProjectile.FlyTime));
            
            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].LoadsField(fld_flytime) && (codes[i + 1].opcode == OpCodes.Ble_Un_S || codes[i + 1].opcode == OpCodes.Ble_Un))
                {
                    Label lbl_new = generator.DefineLabel(), lbl_pop = generator.DefineLabel(), lbl_original = (Label)codes[i + 1].operand;
                    for (int j = i + 2; j < codes.Count; j++)
                    {
                        if (codes[j].labels.Contains(lbl_original))
                        {
                            codes.InsertRange(j, new[]
                            {
                                new(OpCodes.Br_S, lbl_original),
                                new CodeInstruction(OpCodes.Pop).WithLabels(lbl_pop)
                            });
                            break;
                        }
                    }
                    codes[i + 2].WithLabels(lbl_new);
                    codes[i + 1].opcode = OpCodes.Bgt_Un_S;
                    codes[i + 1].operand = lbl_new;
                    codes.InsertRange(i + 2, new[]
                    {
                        new(OpCodes.Ldarg_0),
                        new(OpCodes.Isinst, typeof(CustomProjectileMoveScript)),
                        new(OpCodes.Dup),
                        new(OpCodes.Brfalse_S, lbl_pop),
                        CodeInstruction.LoadField(typeof(CustomProjectileMoveScript), nameof(CustomProjectileMoveScript.initialImpactConsumed)),
                        new(OpCodes.Brfalse_S, lbl_original)
                    });
                    break;
                }
            }
            return codes;
        }
    }
}