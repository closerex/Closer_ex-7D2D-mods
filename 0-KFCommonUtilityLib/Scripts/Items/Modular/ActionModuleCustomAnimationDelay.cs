using KFCommonUtilityLib.Scripts.Attributes;
using static AnimationDelayData;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleCustomAnimationDelay
{
    [MethodTargetPrefix(nameof(ItemAction.OnHoldingUpdate))]
    private bool Prefix_OnHoldingUpdate(ItemAction __instance, ItemActionData _actionData, out AnimationDelays __state)
    {
        __state = AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value];
        if (!__instance.UseAnimation)
            return true;
        var modifiedData = __state;
        modifiedData.RayCast = __instance.Delay;
        AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value] = modifiedData;
        return true;
    }

    [MethodTargetPostfix(nameof(ItemAction.OnHoldingUpdate))]
    private void Postfix_OnHoldingUpdate(ItemAction __instance, ItemActionData _actionData, AnimationDelays __state)
    {
        if (!__instance.UseAnimation)
            return;
        AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value] = __state;
    }

    [MethodTargetPrefix(nameof(ItemAction.IsActionRunning))]
    private bool Prefix_IsActionRunning(ItemAction __instance, ItemActionData _actionData, out AnimationDelays __state)
    {
        __state = AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value];
        if (!__instance.UseAnimation)
            return true;
        var modifiedData = __state;
        modifiedData.RayCast = __instance.Delay * .5f;
        AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value] = modifiedData;
        return true;
    }

    [MethodTargetPostfix(nameof(ItemAction.IsActionRunning))]
    private void Postfix_IsActionRunning(ItemAction __instance, ItemActionData _actionData, AnimationDelays __state)
    {
        if (!__instance.UseAnimation)
            return;
        AnimationDelayData.AnimationDelay[_actionData.invData.item.HoldType.Value] = __state;
    }

    //following are fix for item use time from menu entry
    //when IsActionRunning is called from coroutine which is started by menu entry,
    //as OnHoldingUpdate is not called every frame, the check might yield false before item actually gets consumed, thus returning the item
    //so we call OnHoldingUpdate to properly consume the item
    //vanilla method on the other hand, is forcing double delay in IsActionRunning
    [MethodTargetPostfix(nameof(ItemAction.IsActionRunning), typeof(ItemActionEat))]
    private void Postfix_IsActionRunning_ItemActionEat(ItemActionEat __instance, ItemActionData _actionData, AnimationDelays __state, bool __result)
    {
        Postfix_IsActionRunning(__instance, _actionData, __state);
        if (!__result && ((ItemActionEat.MyInventoryData)_actionData).bEatingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [MethodTargetPostfix(nameof(ItemAction.IsActionRunning), typeof(ItemActionGainSkill))]
    private void Postfix_IsActionRunning_ItemActionGainSkill(ItemActionGainSkill __instance, ItemActionData _actionData, AnimationDelays __state, bool __result)
    {
        Postfix_IsActionRunning(__instance, _actionData, __state);
        if (!__result && ((ItemActionGainSkill.MyInventoryData)_actionData).bReadingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [MethodTargetPostfix(nameof(ItemAction.IsActionRunning), typeof(ItemActionLearnRecipe))]
    private void Postfix_IsActionRunning_ItemActionLearnRecipe(ItemActionLearnRecipe __instance, ItemActionData _actionData, AnimationDelays __state, bool __result)
    {
        Postfix_IsActionRunning(__instance, _actionData, __state);
        if (!__result && ((ItemActionLearnRecipe.MyInventoryData)_actionData).bReadingStarted)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }

    [MethodTargetPostfix(nameof(ItemAction.IsActionRunning), typeof(ItemActionQuest))]
    private void Postfix_IsActionRunning_ItemActionQuest(ItemActionQuest __instance, ItemActionData _actionData, AnimationDelays __state, bool __result)
    {
        Postfix_IsActionRunning(__instance, _actionData, __state);
        if (!__result && ((ItemActionQuest.MyInventoryData)_actionData).bQuestAccept)
        {
            __instance.OnHoldingUpdate(_actionData);
        }
    }
}