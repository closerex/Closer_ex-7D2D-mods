using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;

[TypeTarget(typeof(ItemActionZoom))]
public class ActionModuleInterruptReload
{
    [MethodTargetPrefix(nameof(ItemActionZoom.ExecuteAction))]
    private bool Prefix_ExecuteAction(ItemActionData _actionData, bool _bReleased, ItemActionZoom __instance)
    {
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            if (!_bReleased && !player.AimingGun && !player.IsAimingGunPossible() && !__instance.IsActionRunning(_actionData))
            {
                int actionIndex = MultiActionManager.GetActionIndexForEntity(player);
                var rangedData = player.inventory.holdingItemData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
                if(rangedData != null && rangedData.isReloading && !rangedData.isReloadCancelled)
                {
                    player.inventory.holdingItem.Actions[actionIndex].CancelReload(rangedData);
                    return false;
                }
            }
        }
        return true;
    }
}