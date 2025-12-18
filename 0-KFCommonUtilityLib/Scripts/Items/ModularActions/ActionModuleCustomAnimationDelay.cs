using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using System.Reflection.Emit;
using UAI;
using UniLinq;
using static AnimationDelayData;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleCustomAnimationDelay
{
    public bool tpvUseCustomDelay = false;
    public float customDelay;

    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionGainSkill), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionLearnRecipe), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionQuest), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionThrowAway), nameof(ItemAction.OnHoldingUpdate))]
    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.IsActionRunning))]
    [HarmonyPatch(typeof(ItemActionGainSkill), nameof(ItemAction.IsActionRunning))]
    [HarmonyPatch(typeof(ItemActionLearnRecipe), nameof(ItemAction.IsActionRunning))]
    [HarmonyPatch(typeof(ItemActionQuest), nameof(ItemAction.IsActionRunning))]
    [HarmonyPatch(typeof(ItemActionThrowAway), nameof(ItemAction.IsActionRunning))]
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
                            new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleCustomAnimationDelay>)),
                            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleCustomAnimationDelay>), nameof(IModuleContainerFor<ActionModuleCustomAnimationDelay>.Instance))),
                            new CodeInstruction(OpCodes.Ldarg_1),
                            new CodeInstruction(flag ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0),
                            CodeInstruction.Call(typeof(ActionModuleCustomAnimationDelay), nameof(ActionModuleCustomAnimationDelay.GetDelayOverride))
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
    public bool Prefix_ItemActionEat_OnHoldingUpdate(ItemActionData _actionData, out (bool, float) __state)
    {
        if (Constants.cVersionInformation.GTE(VersionInformation.EGameReleaseType.V, 2, 5))
        {
            __state = (true, AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value].RayCast);
            AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value].RayCast = customDelay;
        }
        else
        {
            __state = (false, 0);
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    public void Postfix_ItemActionEat_OnHoldingUpdate(ItemActionData _actionData, (bool exec, float delay) __state)
    {
        if (__state.exec)
        {
            AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value].RayCast = __state.delay;
        }
    }

    public float GetDelayOverride(ItemActionData actionData, bool conditionCheck)
    {
        if ((actionData.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView) || tpvUseCustomDelay)
        {
            return customDelay;
        }
        return AnimationDelayData.AnimationDelay[actionData.invData.item.HoldType.Value].RayCast * (conditionCheck ? 2f : 1f);
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

    //following are fix for item use time from menu entry
    //when IsActionRunning is called from coroutine which is started by menu entry,
    //as OnHoldingUpdate is not called every frame, the check might yield false before item actually gets consumed, thus returning the item
    //so we call OnHoldingUpdate to properly consume the item
    //vanilla method on the other hand, is forcing double delay in IsActionRunning
    [HarmonyPatch(typeof(ItemActionEat), nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning_ItemActionEat(ItemActionEat __instance, ItemActionData _actionData, bool __result)
    {
        if (!__result && ((ItemActionEat.MyInventoryData)_actionData).bEatingStarted)
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

    //public class CustomAnimationDelayData : AnimationDelayData
    //{
    //    ActionModuleCustomAnimationDelay module;
    //    public CustomAnimationDelayData(ActionModuleCustomAnimationDelay __customModule)
    //    {
    //        this.module = __customModule;
    //    }
    //}
}