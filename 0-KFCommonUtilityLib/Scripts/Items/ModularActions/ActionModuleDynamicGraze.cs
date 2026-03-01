using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using UnityEngine;

[TypeTarget(typeof(ItemActionDynamic))]
public class ActionModuleDynamicGraze
{
    private string dynamicSoundStart = null;

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPrefix]
    private void Prefix_ExecuteAction(ItemActionDynamic __instance, ItemActionData _actionData, bool _bReleased, bool __runOriginal, out (bool executed, string originalSound) __state)
    {
        if (__runOriginal && !_bReleased && !string.IsNullOrEmpty(dynamicSoundStart) && _actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(player);
            if (targets && targets.IsAnimationSet)
            {
                __state = (true, __instance.soundStart);
                __instance.soundStart = dynamicSoundStart;
                return;
            }
        }
        __state = (false, null);
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
    private void Prefix_OnHoldingUpdate(ItemActionDynamic __instance, ItemActionData _actionData, bool __runOriginal, out (bool executed, bool useGrazeCast, float lastUseTime) __state)
    {
        if (__runOriginal && _actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(player);
            if (targets && targets.IsAnimationSet)
            {
                __state = (true, __instance.UseGrazingHits, _actionData.lastUseTime);
                _actionData.lastUseTime = Time.time;
                __instance.UseGrazingHits = false;
                //Log.Out($"Prefix lastUseTime: {_actionData.lastUseTime} item {_actionData.invData.item.GetItemName()} actionIdx {_actionData.indexInEntityOfAction}");
                return;
            }
        }
        __state = (false, false, -1);
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    private void Postfix_OnHoldingUpdate(ItemActionDynamic __instance, ItemActionData _actionData, (bool executed, bool useGrazeCast, float lastUseTime) __state)
    {
        if (__state.executed)
        {
            __instance.UseGrazingHits = __state.useGrazeCast;
            _actionData.lastUseTime = __state.lastUseTime;
            //Log.Out($"Postfix lastUseTime: {_actionData.lastUseTime} item {_actionData.invData.item.GetItemName()} actionIdx {_actionData.indexInEntityOfAction}");
        }
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        _props.ParseString("DynamicSoundStart", ref dynamicSoundStart);
    }
}