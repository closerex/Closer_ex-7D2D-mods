using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UAI;
using static ActionModuleAlternative;

[TypeTarget(typeof(ItemActionRanged), typeof(MultiActionData))]
public class ActionModuleMultiActionFix
{
    [MethodTargetPrefix(nameof(ItemActionRanged.StartHolding))]
    private bool Prefix_StartHolding(ItemActionData _data, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_data, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StartHolding))]
    private void Postfix_StartHolding(ItemActionData _data, ItemActionData __state)
    {
        RestoreItemActionData(_data, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.StopHolding))]
    private bool Prefix_StopHolding(ItemActionData _data, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_data, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StopHolding))]
    private void Postfix_StopHolding(ItemActionData _data, ItemActionData __state)
    {
        RestoreItemActionData(_data, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.ItemActionEffects), typeof(ItemActionLauncher))]
    private bool Prefix_ItemActionEffects(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.ItemActionEffects), typeof(ItemActionLauncher))]
    private void Postfix_ItemActionEffects(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.CancelAction))]
    private bool Prefix_CancelAction(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.CancelAction))]
    private void Postfix_CancelAction(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.CancelReload))]
    private bool Prefix_CancelReload(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.CancelReload))]
    private void Postfix_CancelReload(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.OnHUD))]
    private bool Prefix_OnHUD(ItemActionData _actionData)
    {
        if (_actionData.indexInEntityOfAction != _actionData.invData.holdingEntity.MinEventContext.ItemActionData.indexInEntityOfAction)
            return false;
        return true;
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.ExecuteAction))]
    private bool Prefix_ExecuteAction(ItemActionData _actionData, MultiActionData __customData)
    {
        //when executing action, set last action index so that correct accuracy is used for drawing crosshair
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            ((ItemActionRanged.ItemActionDataRanged)_actionData).lastAccuracy = __customData.lastAccuracy;
        }
        return true;
    }

    [MethodTargetPrefix("updateAccuracy")]
    private bool Prefix_updateAccuracy(ItemActionData _actionData, MultiActionData __customData)
    {
        //always update custom accuracy
        ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
        (rangedData.lastAccuracy, __customData.lastAccuracy) = (__customData.lastAccuracy, rangedData.lastAccuracy);
        return true;
    }

    [MethodTargetPostfix("updateAccuracy")]
    private void Postfix_updateAccuracy(ItemActionData _actionData, MultiActionData __customData)
    {
        //retain rangedData accuracy if it's the last executed action
        ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntityID(player.entityId) == _actionData.indexInEntityOfAction)
        {
            __customData.lastAccuracy = rangedData.lastAccuracy;
        }
        else
        {
            (rangedData.lastAccuracy, __customData.lastAccuracy) = (__customData.lastAccuracy, rangedData.lastAccuracy);
        }
    }

    [MethodTargetPostfix("onHoldingEntityFired")]
    private void Postfix_onHoldingEntityFired(ItemActionData _actionData, MultiActionData __customData)
    {
        //after firing, if it's the last executed action then update custom accuracy
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntityID(player.entityId) == _actionData.indexInEntityOfAction)
        {
            __customData.lastAccuracy = ((ItemActionRanged.ItemActionDataRanged)_actionData).lastAccuracy;
        }
    }

    private static void SetAndSaveItemActionData(ItemActionData _actionData, out ItemActionData lastActionData)
    {
        lastActionData = _actionData.invData.holdingEntity.MinEventContext.ItemActionData;
        _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
    }

    private static void RestoreItemActionData(ItemActionData _actionData, ItemActionData lastActionData)
    {
        _actionData.invData.holdingEntity.MinEventContext.ItemActionData = lastActionData;
        lastActionData = null;
    }

    public class MultiActionData
    {
        public float lastAccuracy;

        public MultiActionData(ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleMultiActionFix _module)
        {

        }
    }
}