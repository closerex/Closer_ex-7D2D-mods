using KFCommonUtilityLib.Scripts.Attributes;

[TypeTarget(typeof(ItemActionDynamic))]
public class ActionModuleDynamicGraze
{
    [MethodTargetPrefix(nameof(ItemAction.OnHoldingUpdate))]
    private bool Prefix_OnHoldingUpdate(ItemActionDynamic __instance, ItemActionData _actionData, out (bool executed, bool useGrazeCast) __state)
    {
        if(_actionData.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView)
        {
            __state = (true, __instance.UseGrazingHits);
            __instance.UseGrazingHits = false;
        }
        else
        {
            __state = (false, false);
        }
        return true;
    }

    [MethodTargetPostfix(nameof(ItemAction.OnHoldingUpdate))]
    private void Postfix_OnHoldingUpdate(ItemActionDynamic __instance, (bool executed, bool useGrazeCast) __state)
    {
        if (__state.executed)
        {
            __instance.UseGrazingHits = __state.useGrazeCast;
        }
    }
}