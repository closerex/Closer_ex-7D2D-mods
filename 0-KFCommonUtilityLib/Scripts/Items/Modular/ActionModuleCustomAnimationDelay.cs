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
}