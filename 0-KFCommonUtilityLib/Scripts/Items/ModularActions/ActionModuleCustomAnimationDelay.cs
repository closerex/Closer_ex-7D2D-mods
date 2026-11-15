using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using UniLinq;
using UnityEngine;
using static AnimationDelayData;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleCustomAnimationDelay
{
    public bool tpvUseCustomDelay;
    public float customDelay;

    [HarmonyPatch(nameof(Transpiler_DelayOverride))]
    [TargetMethods]
    [UsedImplicitly]
    private static IEnumerable<HarmonyPatch> TargetMethods_DelayOverride()
    {
        yield return new(typeof(ItemActionGainSkill), nameof(ItemAction.OnHoldingUpdate));
        yield return new(typeof(ItemActionLearnRecipe), nameof(ItemAction.OnHoldingUpdate));
        yield return new(typeof(ItemActionQuest), nameof(ItemAction.OnHoldingUpdate));
        yield return new(typeof(ItemActionThrowAway), nameof(ItemAction.OnHoldingUpdate));
        yield return new(typeof(ItemActionEat), nameof(ItemAction.IsActionRunning));
        yield return new(typeof(ItemActionGainSkill), nameof(ItemAction.IsActionRunning));
        yield return new(typeof(ItemActionLearnRecipe), nameof(ItemAction.IsActionRunning));
        yield return new(typeof(ItemActionQuest), nameof(ItemAction.IsActionRunning));
        yield return new(typeof(ItemActionThrowAway), nameof(ItemAction.IsActionRunning));
    }

    [MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_DelayOverride(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var fld_delayarr = AccessTools.Field(typeof(AnimationDelayData), nameof(AnimationDelay));
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
                        int start = flag ? i - 1 : i;
                        int count = j - i + (flag ? 3 : 1);
                        var lbls = codes[start].ExtractLabels();
                        codes.RemoveRange(start, count);
                        codes.InsertRange(start, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0).WithLabels(lbls),
                            CodeInstruction.Call(typeof(ModuleExtensions), nameof(ModuleExtensions.LoadModule), null, new []{typeof(ActionModuleCustomAnimationDelay)}),
                            new(OpCodes.Ldarg_1),
                            new(flag ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0),
                            CodeInstruction.Call(typeof(ActionModuleCustomAnimationDelay), nameof(GetDelayOverride))
                        });
                        break;
                    }
                }
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.OnHoldingUpdate)), MethodTargetPrefix]
    public void Prefix_ItemActionEat_OnHoldingUpdate(ItemActionData _actionData, out (bool exec, int holdType, float delay) __state, bool __runOriginal)
    {
        if (__runOriginal)
        {
            int holdType = _actionData.invData.item.HoldType.Value;
            __state = (true, holdType, AnimationDelay[holdType].RayCast);
            AnimationDelay[holdType].RayCast = customDelay;
        }
        else
        {
            __state = (false, 0, 0f);
        }
    }

    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    public void Postfix_ItemActionEat_OnHoldingUpdate(ItemActionData _actionData, (bool exec, int holdType, float delay) __state)
    {
        if (__state.exec)
        {
            AnimationDelay[__state.holdType].RayCast = __state.delay;
        }
    }

    public float GetDelayOverride(ItemActionData actionData, bool conditionCheck)
    {
        if ((actionData.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView) || tpvUseCustomDelay)
        {
            return customDelay;
        }
        return AnimationDelay[actionData.invData.item.HoldType.Value].RayCast * (conditionCheck ? 2f : 1f);
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(ItemAction __instance, DynamicProperties _props)
    {
        tpvUseCustomDelay = false;
        _props.ParseBool("tpvUseCustomDelay", ref tpvUseCustomDelay);
        customDelay = __instance.Delay;
        _props.ParseFloat("CustomAnimationDelay", ref customDelay);
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(ItemActionData _data)
    {
        _data.lastUseTime = 0f;
    }

    // Menu simulation updates once before polling; finish pending effects from the poll.
    // Eat stays running until consumed; the other actions can time out first.
    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionEat(ItemActionEat __instance, ItemActionData _actionData)
    {
        if (((ItemActionEat.MyInventoryData)_actionData).bEatingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [HarmonyPatch(typeof(ItemActionGainSkill), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionGainSkill(ItemActionGainSkill __instance, ItemActionData _actionData, bool __result)
    {
        if (!__result && ((ItemActionGainSkill.MyInventoryData)_actionData).bReadingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [HarmonyPatch(typeof(ItemActionLearnRecipe), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionLearnRecipe(ItemActionLearnRecipe __instance, ItemActionData _actionData, bool __result)
    {
        if (!__result && ((ItemActionLearnRecipe.MyInventoryData)_actionData).bReadingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [HarmonyPatch(typeof(ItemActionQuest), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionQuest(ItemActionQuest __instance, ItemActionData _actionData, bool __result)
    {
        if (!__result && ((ItemActionQuest.MyInventoryData)_actionData).bQuestAccept)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [HarmonyPatch(typeof(ItemActionActivate), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_ItemActionActivate_IsActionRunning(ItemActionActivate __instance, ItemActionData _actionData, ref bool __result)
    {
        __result |= Time.time - _actionData.lastUseTime < __instance.Delay;
    }
}
