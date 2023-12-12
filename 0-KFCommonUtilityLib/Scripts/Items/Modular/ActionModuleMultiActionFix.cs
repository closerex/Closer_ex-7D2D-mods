using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TypeTarget(typeof(ItemActionRanged), typeof(MultiActionData))]
public class ActionModuleMultiActionFix
{
    private const string LAST_ACTION_INDEX_NAME = ".LastActionIndex";
    [MethodTargetPrefix(nameof(ItemActionRanged.AllowItemLoopingSound))]
    private bool Prefix_AllowItemLoopinigSound(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.AllowItemLoopingSound))]
    private void Postfix_AllowItemLoopingSound(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.ExecuteAction))]
    private bool Prefix_ExecuteAction(ItemActionData _actionData, MultiActionData __customData)
    {
        //when executing action, set last action index so that correct accuracy is used for drawing crosshair
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            player.SetCVar(LAST_ACTION_INDEX_NAME, _actionData.indexInEntityOfAction);
            ((ItemActionRanged.ItemActionDataRanged)_actionData).lastAccuracy = __customData.lastAccuracy;
        }
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.ExecuteAction))]
    private void Postfix_ExecuteAction(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.GetCameraShakeType))]
    private bool Prefix_GetCameraShakeType(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.GetCameraShakeType))]
    private void Postfix_GetCameraShakeType(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.GetCrosshairType))]
    private bool Prefix_GetCrosshairType(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.GetCrosshairType))]
    private void Postfix_GetCrosshairType(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.GetFocusType))]
    private bool Prefix_GetFocusType(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.GetFocusType))]
    private void Postfix_GetFocusType(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.IsActionRunning))]
    private bool Prefix_IsActionRunning(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.IsActionRunning))]
    private void Postfix_IsActionRunning(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.IsAimingGunPossible))]
    private bool Prefix_IsAimingGunPossible(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.IsAimingGunPossible))]
    private void Postfix_IsAimingGunPossible(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.OnHoldingUpdate))]
    private bool Prefix_OnHoldingUpdate(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.OnHoldingUpdate))]
    private void Postfix_OnHoldingUpdate(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.StartHolding))]
    private bool Prefix_StartHolding(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StartHolding))]
    private void Postfix_StartHolding(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            player.SetCVar(LAST_ACTION_INDEX_NAME, 0);
        }
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.StopHolding))]
    private bool Prefix_StopHolding(ItemActionData _actionData)
    {
        MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StopHolding))]
    private void Postfix_StopHolding(ItemActionData _actionData)
    {
        MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            player.SetCVar(LAST_ACTION_INDEX_NAME, 0);
        }
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
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player && (int)player.GetCVar(LAST_ACTION_INDEX_NAME) == _actionData.indexInEntityOfAction)
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
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player && (int)player.GetCVar(LAST_ACTION_INDEX_NAME) == _actionData.indexInEntityOfAction)
        {
            __customData.lastAccuracy = ((ItemActionRanged.ItemActionDataRanged)_actionData).lastAccuracy;
        }
    }


    public class MultiActionData
    {
        public float lastAccuracy;

        public MultiActionData(ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleMultiActionFix _module)
        {

        }
    }
}