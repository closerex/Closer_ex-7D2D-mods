using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;
using UnityEngine;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    internal static class ReloadInterruptionPatches
    {
        //interrupt reload with firing
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction))]
        [HarmonyPrefix]
        private static bool Prefix_ExecuteAction_ItemClass(ItemClass __instance, int _actionIdx, ItemInventoryData _data, bool _bReleased, PlayerActionsLocal _playerActions)
        {
            ItemAction curAction = __instance.Actions[_actionIdx];
            if (curAction is ItemActionRanged || curAction is ItemActionZoom)
            {
                int curActionIndex = MultiActionManager.GetActionIndexForEntity(_data.holdingEntity);
                var rangedAction = __instance.Actions[curActionIndex] as ItemActionRanged;
                var rangedData = _data.actionData[curActionIndex] as ItemActionRanged.ItemActionDataRanged;
                if (rangedData != null && rangedData is IModuleContainerFor<ActionModuleInterruptReload.InterruptData> dataModule && rangedAction is IModuleContainerFor<ActionModuleInterruptReload> actionModule)
                {
                    if (!_bReleased && _playerActions != null && ((EntityPlayerLocal)_data.holdingEntity).bFirstPersonView && ((_playerActions.Primary.IsPressed && _actionIdx == curActionIndex && _data.itemValue.Meta > 0) || (_playerActions.Secondary.IsPressed && curAction is ItemActionZoom)) && (rangedData.isReloading || rangedData.isWeaponReloading) && !dataModule.Instance.isInterruptRequested)
                    {
                        if (dataModule.Instance.holdStartTime < 0)
                        {
                            dataModule.Instance.holdStartTime = Time.time;
                            return false;
                        }
                        if (Time.time - dataModule.Instance.holdStartTime >= actionModule.Instance.holdBeforeCancel)
                        {
                            if (!rangedAction.reloadCancelled(rangedData))
                            {
                                rangedAction.CancelReload(rangedData);
                            }
                            if (ConsoleCmdReloadLog.LogInfo)
                                Log.Out($"interrupt requested!");
                            dataModule.Instance.isInterruptRequested = true;
                            if (actionModule.Instance.instantFiringCancel && curAction is ItemActionRanged)
                            {
                                if (ConsoleCmdReloadLog.LogInfo)
                                    Log.Out($"instant firing cancel!");
                                dataModule.Instance.instantFiringRequested = true;
                                return true;
                            }
                        }
                        return false;
                    }
                    if (_bReleased)
                    {
                        dataModule.Instance.Reset();
                    }
                }
            }
            return true;
        }

        [HarmonyPatch(typeof(ItemAction), nameof(ItemAction.CancelReload))]
        [HarmonyPrefix]
        private static bool Prefix_CancelReload_ItemAction(ItemActionData _actionData)
        {
            if (_actionData?.invData?.holdingEntity is EntityPlayerLocal && AnimationRiggingManager.IsHoldingRiggedWeapon)
            {
                return false;
            }
            return true;
        }
    }
}
