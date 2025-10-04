using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;

[TypeTarget(typeof(ItemActionDynamic))]
public class ActionModuleDynamicGraze
{
    private string dynamicSoundStart = null;

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPrefix]
    private bool Prefix_ExecuteAction(ItemActionDynamic __instance, ItemActionData _actionData, bool _bReleased, out (bool executed, string originalSound) __state)
    {
        if (!_bReleased && !string.IsNullOrEmpty(dynamicSoundStart) && _actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(player);
            if (targets && targets.IsAnimationSet)
            {
                __state = (true, __instance.soundStart);
                __instance.soundStart = dynamicSoundStart;
                return true;
            }
        }
        __state = (false, null);
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPostfix]
    private void Postfix_ExecuteAction(ItemActionDynamic __instance, (bool executed, string originalSound) __state)
    {
        if (__state.executed)
        {
            __instance.soundStart = __state.originalSound;
        }
    }

    [HarmonyPatch(nameof(ItemAction.CancelAction))]
    [HarmonyPostfix]
    public void Postfix_CancelAction(ItemActionData _actionData)
    {
        _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool("IsMeleeRunning", false);
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPrefix]
    private bool Prefix_OnHoldingUpdate(ItemActionDynamic __instance, ItemActionData _actionData, out (bool executed, bool useGrazeCast) __state)
    {
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(player);
            if (targets && targets.IsAnimationSet)
            {
                __state = (true, __instance.UseGrazingHits);
                __instance.UseGrazingHits = false;
                return true;
            }
        }
        __state = (false, false);
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    private void Postfix_OnHoldingUpdate(ItemActionDynamic __instance, (bool executed, bool useGrazeCast) __state)
    {
        if (__state.executed)
        {
            __instance.UseGrazingHits = __state.useGrazeCast;
        }
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        _props.ParseString("DynamicSoundStart", ref dynamicSoundStart);
    }
}