using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using UnityEngine;

[TypeTarget(typeof(ItemAction), typeof(ActionModuleInterruptReload.InterruptData))]
public class ActionModuleInterruptReload
{
    //[MethodTargetPrefix(nameof(ItemActionZoom.ExecuteAction), typeof(ItemActionZoom))]
    //private bool Prefix_ExecuteAction(ItemActionData _actionData, bool _bReleased, ItemActionZoom __instance, InterruptData __customData)
    //{
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
    //    {
    //        if (!_bReleased && !player.AimingGun && !player.IsAimingGunPossible() && !__instance.IsActionRunning(_actionData))
    //        {
    //            int actionIndex = MultiActionManager.GetActionIndexForEntity(player);
    //            var rangedData = player.inventory.holdingItemData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
    //            if(rangedData != null && rangedData.isReloading && !rangedData.isReloadCancelled && !__customData.isInterruptRequested)
    //            {
    //                player.inventory.holdingItem.Actions[actionIndex].CancelReload(rangedData);
    //                __customData.isInterruptRequested = true;
    //            }
    //        }
    //        else if (_bReleased)
    //        {
    //            __customData.isInterruptRequested = false;
    //            Log.Out($"interrupt cancel\n{StackTraceUtility.ExtractStackTrace()}");
    //        }
    //    }
    //    return true;
    //}

    public class InterruptData
    {
        public bool isInterruptRequested;

        public InterruptData(ItemInventoryData invData, int actionIndex, ActionModuleInterruptReload module)
        {

        }
    }
}