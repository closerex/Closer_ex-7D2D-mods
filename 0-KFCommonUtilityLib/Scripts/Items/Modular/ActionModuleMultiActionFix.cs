using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;

[TypeTarget(typeof(ItemActionAttack), typeof(MultiActionData))]
public class ActionModuleMultiActionFix
{
    [MethodTargetPrefix(nameof(ItemActionAttack.StartHolding))]
    private bool Prefix_StartHolding(ItemActionData _data, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_data, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionAttack.StartHolding))]
    private void Postfix_StartHolding(ItemActionData _data, ItemActionData __state)
    {
        RestoreItemActionData(_data, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.StopHolding))]
    private bool Prefix_StopHolding(ItemActionData _data, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_data, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionAttack.StopHolding))]
    private void Postfix_StopHolding(ItemActionData _data, ItemActionData __state)
    {
        RestoreItemActionData(_data, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.ItemActionEffects), typeof(ItemActionLauncher))]
    private bool Prefix_ItemActionEffects(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionAttack.ItemActionEffects), typeof(ItemActionLauncher))]
    private void Postfix_ItemActionEffects(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.CancelAction))]
    private bool Prefix_CancelAction(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionAttack.CancelAction))]
    private void Postfix_CancelAction(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.CancelReload))]
    private bool Prefix_CancelReload(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionAttack.CancelReload))]
    private void Postfix_CancelReload(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.ReloadGun))]
    private bool Prefix_ReloadGun(ItemActionData _actionData)
    {
        //int reloadAnimationIndex = MultiActionManager.GetMetaIndexForActionIndex(_actionData.invData.holdingEntity.entityId, _actionData.indexInEntityOfAction);
        _actionData.invData.holdingEntity.emodel?.avatarController?.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, _actionData.indexInEntityOfAction, false);
        _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
        //MultiActionManager.GetMappingForEntity(_actionData.invData.holdingEntity.entityId)?.SaveMeta();
        return true;
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.OnHUD))]
    private bool Prefix_OnHUD(ItemActionData _actionData)
    {
        if (_actionData.indexInEntityOfAction != _actionData.invData.holdingEntity.MinEventContext.ItemActionData.indexInEntityOfAction)
            return false;
        return true;
    }

    //[MethodTargetPrefix(nameof(ItemActionAttack.ExecuteAction), typeof(ItemActionRanged))]
    //private bool Prefix_ExecuteAction(ItemActionData _actionData, MultiActionData __customData)
    //{
    //    //when executing action, set last action index so that correct accuracy is used for drawing crosshair
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
    //    {
    //        ((ItemActionRanged.ItemActionDataRanged)_actionData).lastAccuracy = __customData.lastAccuracy;
    //    }
    //    return true;
    //}

    //[MethodTargetPrefix("updateAccuracy", typeof(ItemActionRanged))]
    //private bool Prefix_updateAccuracy(ItemActionData _actionData, MultiActionData __customData)
    //{
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntityID(player.entityId) == _actionData.indexInEntityOfAction)
    //        return true;
    //    //always update custom accuracy
    //    ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
    //    (rangedData.lastAccuracy, __customData.lastAccuracy) = (__customData.lastAccuracy, rangedData.lastAccuracy);
    //    return true;
    //}

    //[MethodTargetPostfix("updateAccuracy", typeof(ItemActionRanged))]
    //private void Postfix_updateAccuracy(ItemActionData _actionData, MultiActionData __customData)
    //{
    //    //retain rangedData accuracy if it's the last executed action
    //    ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntityID(player.entityId) == _actionData.indexInEntityOfAction)
    //    {
    //        __customData.lastAccuracy = rangedData.lastAccuracy;
    //    }
    //    else
    //    {
    //        (rangedData.lastAccuracy, __customData.lastAccuracy) = (__customData.lastAccuracy, rangedData.lastAccuracy);
    //    }
    //}
    [MethodTargetPrefix("onHoldingEntityFired", typeof(ItemActionRanged))]
    private bool Prefix_onHoldingEntityFired(ItemActionData _actionData)
    {
        if (!_actionData.invData.holdingEntity.isEntityRemote)
        {
            _actionData.invData.holdingEntity?.emodel?.avatarController.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, _actionData.indexInEntityOfAction);
            //_actionData.invData.holdingEntity?.emodel?.avatarController.CancelEvent("WeaponFire");
        }
        return true;
    }

    //[MethodTargetPostfix("onHoldingEntityFired", typeof(ItemActionRanged))]
    //private void Postfix_onHoldingEntityFired(ItemActionData _actionData, MultiActionData __customData)
    //{
    //    //after firing, if it's the last executed action then update custom accuracy
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntityID(player.entityId) == _actionData.indexInEntityOfAction)
    //    {
    //        __customData.lastAccuracy = ((ItemActionRanged.ItemActionDataRanged)_actionData).lastAccuracy;
    //    }
    //}

    private static void SetAndSaveItemActionData(ItemActionData _actionData, out ItemActionData lastActionData)
    {
        lastActionData = _actionData.invData.holdingEntity.MinEventContext.ItemActionData;
        _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
    }

    private static void RestoreItemActionData(ItemActionData _actionData, ItemActionData lastActionData)
    {
        if (lastActionData != null)
            _actionData.invData.holdingEntity.MinEventContext.ItemActionData = lastActionData;
    }

    public class MultiActionData
    {
        public float lastAccuracy;

        public MultiActionData(ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleMultiActionFix _module)
        {

        }
    }
}