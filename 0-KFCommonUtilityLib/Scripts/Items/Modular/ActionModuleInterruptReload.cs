using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged), typeof(InterruptData))]
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

    public float holdBeforeCancel = 0.06f;
    public string firingStateName = "";
    public bool instantFiringCancel = false;

    [MethodTargetPrefix(nameof(ItemActionRanged.StartHolding))]
    private bool Prefix_StartHolding(InterruptData __customData)
    {
        __customData.Reset();
        return true;
    }

    [MethodTargetPostfix(nameof(ItemAction.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        firingStateName = _props.GetString("FiringStateFullName");
        instantFiringCancel = _props.GetBool("InstantFiringCancel");
    }

    private struct State
    {
        public bool executed;
        public bool isReloading;
        public bool isWeaponReloading;
        public float lastShotTime;
    }

    [MethodTargetPostfix(nameof(ItemAction.IsActionRunning))]
    private void Postfix_IsActionRunning(ref bool __result, InterruptData __customData)
    {
        __result &= !__customData.instantFiringRequested;
    }

    [MethodTargetPrefix(nameof(ItemAction.ExecuteAction))]
    private bool Prefix_ExecuteAction(ItemActionData _actionData, bool _bReleased, InterruptData __customData, out State __state)
    {
        __state = default;
        if (!_bReleased && __customData.isInterruptRequested && __customData.instantFiringRequested)
        {
            if (_actionData.invData.itemValue.Meta > 0)
            {
                if (ConsoleCmdReloadLog.LogInfo)
                    Log.Out($"instant firing cancel prefix!");
                ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
                __state.executed = true;
                __state.isReloading = rangedData.isReloading;
                __state.isWeaponReloading = rangedData.isWeaponReloading;
                __state.lastShotTime = rangedData.m_LastShotTime;
                rangedData.isReloading = false;
                rangedData.isWeaponReloading = false;
            }
            else
            {
                if (ConsoleCmdReloadLog.LogInfo)
                    Log.Out($"not fired! meta is 0");
                __customData.isInterruptRequested = false;
                __customData.instantFiringRequested = false;
                return false;
            }
        }
        return true;
    }

    [MethodTargetPostfix(nameof(ItemAction.ExecuteAction))]
    private void Postfix_ExecuteAction(ItemActionData _actionData, InterruptData __customData, State __state)
    {
        if (__state.executed)
        {
            if (ConsoleCmdReloadLog.LogInfo)
                Log.Out($"instant firing cancel postfix!");
            ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
            rangedData.isReloading = __state.isReloading;
            rangedData.isWeaponReloading = __state.isWeaponReloading;
            if (__customData.itemAnimator != null && __customData.eventBridge != null)
            {
                if (rangedData.m_LastShotTime > __state.lastShotTime && rangedData.m_LastShotTime < Time.time + 1f)
                {
                    if (ConsoleCmdReloadLog.LogInfo)
                        Log.Out($"executed!");
                    __customData.eventBridge.OnReloadEnd();
                    __customData.itemAnimator.Play(firingStateName, -1, 0f);
                }
                else
                {
                    if (ConsoleCmdReloadLog.LogInfo)
                        Log.Out($"not fired! last shot time {__state.lastShotTime} ranged data shot time {rangedData.m_LastShotTime} cur time {Time.time}");
                    __customData.isInterruptRequested = false;
                    __customData.instantFiringRequested = false;
                }
            }
        }
    }

    public class InterruptData
    {
        public bool isInterruptRequested;
        public float holdStartTime = -1f;
        public bool instantFiringRequested = false;
        public AnimationReloadEvents eventBridge;
        public Animator itemAnimator;

        public InterruptData(ItemInventoryData invData, int actionIndex, ActionModuleInterruptReload module)
        {
            if (invData.model && invData.model.TryGetComponent<RigTargets>(out var targets) && !targets.Destroyed)
            {
                eventBridge = targets.itemFpv?.GetComponentInChildren<AnimationReloadEvents>(true);
                itemAnimator = eventBridge?.GetComponent<Animator>();
            }
        }

        public void Reset()
        {
            isInterruptRequested = false;
            holdStartTime = -1f;
            instantFiringRequested = false;
        }
    }
}