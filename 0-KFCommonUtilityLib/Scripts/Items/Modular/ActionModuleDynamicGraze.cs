using KFCommonUtilityLib.Scripts.Attributes;

[TypeTarget(typeof(ItemActionDynamic))]
public class ActionModuleDynamicGraze
{
    private string dynamicSoundStart = null;

    [MethodTargetPrefix(nameof(ItemAction.ExecuteAction))]
    private bool Prefix_ExecuteAction(ItemActionDynamic __instance, ItemActionData _actionData, bool _bReleased, out (bool executed, string originalSound) __state)
    {
        if (!_bReleased && !string.IsNullOrEmpty(dynamicSoundStart) && _actionData.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView)
        {
            __state = (true, __instance.soundStart);
            __instance.soundStart = dynamicSoundStart;
        }
        else
        {
            __state = (false, null);
        }
        return true;
    }

    [MethodTargetPostfix(nameof(ItemAction.ExecuteAction))]
    private void Postfix_ExecuteAction(ItemActionDynamic __instance, (bool executed, string originalSound) __state)
    {
        if (__state.executed)
        {
            __instance.soundStart = __state.originalSound;
        }
    }

    [MethodTargetPrefix(nameof(ItemAction.OnHoldingUpdate))]
    private bool Prefix_OnHoldingUpdate(ItemActionDynamic __instance, ItemActionData _actionData, out (bool executed, bool useGrazeCast) __state)
    {
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView)
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

    [MethodTargetPostfix(nameof(ItemAction.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        _props.ParseString("DynamicSoundStart", ref dynamicSoundStart);
    }
}