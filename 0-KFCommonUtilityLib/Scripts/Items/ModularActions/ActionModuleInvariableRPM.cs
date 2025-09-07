using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;

[TypeTarget(typeof(ItemActionRanged))]
public class ActionModuleInvariableRPM
{
    //added as a transpiler so that it's applied before all post processing
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemAction.OnHoldingUpdate)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_OnHoldingUpdate_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_getvalue))
            {
                int start = -1;
                for (int j = i; j >= 0; j--)
                {
                    if (codes[j].opcode == OpCodes.Stloc_0)
                    {
                        start = j + 2;
                        break;
                    }
                }
                if (start >= 0)
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        CodeInstruction.Call(typeof(ActionModuleInvariableRPM), nameof(CalcFixedRPM))
                    });
                    codes.RemoveRange(start, i - start + 2);
                    //Log.Out("Invariable RPM Patch applied!");
                }
                break;
            }
        }

        return codes;
    }

    private static float CalcFixedRPM(ItemActionRanged rangedAction, ItemActionRanged.ItemActionDataRanged rangedData)
    {
        float rpm = 60f / rangedData.OriginalDelay;
        float perc = 1f;
        var tags = rangedData.invData.item.ItemTags;
        MultiActionManager.ModifyItemTags(rangedData.invData.itemValue, rangedData, ref tags);
        rangedData.invData.item.Effects.ModifyValue(rangedData.invData.holdingEntity, PassiveEffects.RoundsPerMinute, ref rpm, ref perc, rangedData.invData.itemValue.Quality, tags);
        //Log.Out($"fixed RPM {res}");
        return 60f / (rpm * perc);
    }
}