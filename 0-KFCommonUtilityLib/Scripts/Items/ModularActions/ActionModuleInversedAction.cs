using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using UniLinq;
using System.Reflection.Emit;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleInversedAction
{
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.fireShot)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_fireShot(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_fireevent = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));

        for (int i = 4; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fireevent))
            {
                CodeInstruction ins_secondary = codes[i - 4];
                CodeInstruction ins_primary = codes[i - 2];
                if (ins_secondary.opcode == OpCodes.Ldc_I4_S && ins_primary.opcode == OpCodes.Ldc_I4_S &&
                    ((ins_secondary.OperandIs((int)MinEventTypes.onSelfSecondaryActionRayMiss) && ins_primary.OperandIs((int)MinEventTypes.onSelfPrimaryActionRayMiss)) || 
                     (ins_secondary.OperandIs((int)MinEventTypes.onSelfSecondaryActionRayHit) && ins_primary.OperandIs((int)MinEventTypes.onSelfPrimaryActionRayHit))))
                {
                    (ins_primary.operand, ins_secondary.operand) = (ins_secondary.operand, ins_primary.operand);
                }
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ExecuteAction(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_fireevent = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));
        for (int i = 4; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fireevent))
            {
                CodeInstruction ins_secondary = codes[i - 4];
                CodeInstruction ins_primary = codes[i - 2];
                if (ins_secondary.opcode == OpCodes.Ldc_I4_S && ins_primary.opcode == OpCodes.Ldc_I4_S && ins_secondary.OperandIs((int)MinEventTypes.onSelfSecondaryActionMissEntity) && ins_primary.OperandIs((int)MinEventTypes.onSelfPrimaryActionMissEntity))
                {
                    (ins_primary.operand, ins_secondary.operand) = (ins_secondary.operand, ins_primary.operand);
                }
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemActionDynamic.hitTarget)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamic_hitTarget(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_fireevent = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));
        for (int i = 4; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fireevent))
            {
                CodeInstruction ins_secondary = codes[i - 4];
                CodeInstruction ins_primary = codes[i - 2];
                if (ins_secondary.opcode == OpCodes.Ldc_I4_S && ins_primary.opcode == OpCodes.Ldc_I4_S &&
                    ((ins_secondary.OperandIs((int)MinEventTypes.onSelfSecondaryActionGrazeHit) && ins_primary.OperandIs((int)MinEventTypes.onSelfPrimaryActionGrazeHit)) ||
                     (ins_secondary.OperandIs((int)MinEventTypes.onSelfSecondaryActionRayHit) && ins_primary.OperandIs((int)MinEventTypes.onSelfPrimaryActionRayHit))))
                {
                    (ins_primary.operand, ins_secondary.operand) = (ins_secondary.operand, ins_primary.operand);
                }
            }
        }
        return codes;
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.Raycast)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamicMelee_Raycast(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_fireevent = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));
        for (int i = 4; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fireevent))
            {
                CodeInstruction ins_secondary = codes[i - 4];
                CodeInstruction ins_primary = codes[i - 2];
                if (ins_secondary.opcode == OpCodes.Ldc_I4_S && ins_primary.opcode == OpCodes.Ldc_I4_S && ins_secondary.OperandIs((int)MinEventTypes.onSelfSecondaryActionRayMiss) && ins_primary.OperandIs((int)MinEventTypes.onSelfPrimaryActionRayMiss))
                {
                    (ins_primary.operand, ins_secondary.operand) = (ins_secondary.operand, ins_primary.operand);
                }
            }
        }
        return codes;
    }
}