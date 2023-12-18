using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TypeTarget(typeof(ItemActionRanged), typeof(MultiActionData))]
public class ActionModuleMultiActionFix
{
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
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntity(player.entityId) == _actionData.indexInEntityOfAction)
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
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntity(player.entityId) == _actionData.indexInEntityOfAction)
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