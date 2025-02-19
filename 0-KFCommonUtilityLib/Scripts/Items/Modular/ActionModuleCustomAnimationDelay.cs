using HarmonyLib;
using KFCommonUtilityLib.Scripts.Attributes;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using static AnimationDelayData;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleCustomAnimationDelay
{
    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionGainSkill), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionLearnRecipe), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionQuest), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.IsActionRunning))]
    [HarmonyPatch(typeof(ItemActionGainSkill), nameof(ItemAction.IsActionRunning))]
    [HarmonyPatch(typeof(ItemActionLearnRecipe), nameof(ItemAction.IsActionRunning))]
    [HarmonyPatch(typeof(ItemActionQuest), nameof(ItemAction.IsActionRunning))]
    [MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_OnHoldingUpdate(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var fld_delayarr = AccessTools.Field(typeof(AnimationDelayData), nameof(AnimationDelayData.AnimationDelay));
        var fld_raycast = AccessTools.Field(typeof(AnimationDelays), nameof(AnimationDelays.RayCast));

        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_delayarr))
            {
                for (int j = i + 1; j < codes.Count; j++)
                {
                    if (codes[j].LoadsField(fld_raycast))
                    {
                        bool flag = codes[i - 1].LoadsConstant(2f);
                        codes.RemoveRange(flag ? i - 1 : i, j - i + (flag ? 3 : 1));
                        codes.InsertRange(flag ? i - 1 : i, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            CodeInstruction.LoadField(typeof(ItemAction), nameof(ItemAction.Delay))
                        });
                        break;
                    }
                }
                break;
            }
        }

        return codes;
    }

    //[HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPrefix]
    //private bool Prefix_OnHoldingUpdate(ItemAction __instance, ItemActionData _actionData, out AnimationDelays __state)
    //{
    //    __state = AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value];
    //    if (!__instance.UseAnimation)
    //        return true;
    //    var modifiedData = __state;
    //    modifiedData.RayCast = __instance.Delay;
    //    AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value] = modifiedData;
    //    return true;
    //}

    //[HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    //private void Postfix_OnHoldingUpdate(ItemAction __instance, ItemActionData _actionData, AnimationDelays __state)
    //{
    //    if (!__instance.UseAnimation)
    //        return;
    //    AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value] = __state;
    //}

    //[HarmonyPatch(nameof(ItemAction.IsActionRunning)), MethodTargetPrefix]
    //private bool Prefix_IsActionRunning(ItemAction __instance, ItemActionData _actionData, out AnimationDelays __state)
    //{
    //    __state = AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value];
    //    if (!__instance.UseAnimation)
    //        return true;
    //    var modifiedData = __state;
    //    modifiedData.RayCast = __instance.Delay * .5f;
    //    AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value] = modifiedData;
    //    return true;
    //}

    //[HarmonyPatch(nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    //private void Postfix_IsActionRunning(ItemAction __instance, ItemActionData _actionData, AnimationDelays __state)
    //{
    //    if (!__instance.UseAnimation)
    //        return;
    //    AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value] = __state;
    //}

    //following are fix for item use time from menu entry
    //when IsActionRunning is called from coroutine which is started by menu entry,
    //as OnHoldingUpdate is not called every frame, the check might yield false before item actually gets consumed, thus returning the item
    //so we call OnHoldingUpdate to properly consume the item
    //vanilla method on the other hand, is forcing double delay in IsActionRunning
    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionEat(ItemActionEat __instance, ItemActionData _actionData/*, AnimationDelays __state*/, bool __result)
    {
        //Postfix_IsActionRunning(__instance, _actionData, __state);
        if (!__result && ((ItemActionEat.MyInventoryData)_actionData).bEatingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [HarmonyPatch(typeof(ItemActionGainSkill), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionGainSkill(ItemActionGainSkill __instance, ItemActionData _actionData/*, AnimationDelays __state*/, bool __result)
    {
        //Postfix_IsActionRunning(__instance, _actionData, __state);
        if (!__result && ((ItemActionGainSkill.MyInventoryData)_actionData).bReadingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [HarmonyPatch(typeof(ItemActionLearnRecipe), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionLearnRecipe(ItemActionLearnRecipe __instance, ItemActionData _actionData/*, AnimationDelays __state*/, bool __result)
    {
        //Postfix_IsActionRunning(__instance, _actionData, __state);
        if (!__result && ((ItemActionLearnRecipe.MyInventoryData)_actionData).bReadingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [HarmonyPatch(typeof(ItemActionQuest), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionQuest(ItemActionQuest __instance, ItemActionData _actionData/*, AnimationDelays __state*/, bool __result)
    {
        //Postfix_IsActionRunning(__instance, _actionData, __state);
        if (!__result && ((ItemActionQuest.MyInventoryData)_actionData).bQuestAccept)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }
}