using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), ActionDataTarget(typeof(InterruptData))]
public class ActionModuleInterruptReload
{
    public float holdBeforeCancel = 0.06f;
    public string firingStateName = "";
    public bool instantFiringCancel = false;

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPrefix]
    private bool Prefix_StartHolding(InterruptData __customData)
    {
        __customData.Reset();
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        firingStateName = _props.GetString("FiringStateFullName");
        instantFiringCancel = _props.GetBool("InstantFiringCancel");
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationsChanged(ItemActionData _data, InterruptData __customData)
    {
        var invData = _data.invData;
        __customData.itemAnimator = AnimationGraphBuilder.DummyWrapper;
        __customData.eventBridge = null;
        if (invData.model && invData.model.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed && targets.IsAnimationSet)
        {
            __customData.itemAnimator = targets.GraphBuilder.WeaponWrapper;
            if (__customData.itemAnimator.IsValid)
            {
                __customData.eventBridge = targets.ItemAnimator.GetComponent<AnimationReloadEvents>();
            }
        }
    }

    private struct State
    {
        public bool executed;
        public bool isReloading;
        public bool isWeaponReloading;
        public float lastShotTime;
    }

    [HarmonyPatch(nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning(ref bool __result, InterruptData __customData)
    {
        __result &= !__customData.instantFiringRequested;
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPrefix]
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

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPostfix]
    private void Postfix_ExecuteAction(ItemActionData _actionData, InterruptData __customData, State __state)
    {
        if (__state.executed)
        {
            if (ConsoleCmdReloadLog.LogInfo)
                Log.Out($"instant firing cancel postfix!");
            ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
            rangedData.isReloading = __state.isReloading;
            rangedData.isWeaponReloading = __state.isWeaponReloading;
            if (__customData.itemAnimator.IsValid && __customData.eventBridge)
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

    [HarmonyPatch(nameof(ItemAction.ItemActionEffects)), MethodTargetPrefix]
    private bool Prefix_ItemActionEffects(ItemActionData _actionData, int _firingState, InterruptData __customData)
    {
        var rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
        if (_firingState != 0 && (rangedData.isReloading || rangedData.isWeaponReloading) && !(rangedData.invData.holdingEntity is EntityPlayerLocal) && __customData.eventBridge)
        {
            __customData.eventBridge.OnReloadEnd();
            __customData.itemAnimator.Play(firingStateName, -1, 0f);
        }
        return true;
    }

    public bool IsRequestPossible(InterruptData interruptData)
    {
        return interruptData.eventBridge && interruptData.itemAnimator.IsValid;
    }

    public class InterruptData
    {
        public bool isInterruptRequested;
        public float holdStartTime = -1f;
        public bool instantFiringRequested = false;
        public AnimationReloadEvents eventBridge;
        public IAnimatorWrapper itemAnimator;

        public InterruptData(ItemInventoryData invData, int actionIndex, ActionModuleInterruptReload module)
        {
            //if (invData.model && invData.model.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
            //{
            //    itemAnimator = targets.ItemAnimator;
            //    if (itemAnimator)
            //    {
            //        eventBridge = itemAnimator.GetComponent<AnimationReloadEvents>();
            //    }
            //}
        }

        public void Reset()
        {
            isInterruptRequested = false;
            holdStartTime = -1f;
            instantFiringRequested = false;
        }
    }
}

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
                if (!_bReleased && _playerActions != null && actionModule.Instance.IsRequestPossible(dataModule.Instance) && ((_playerActions.Primary.IsPressed && _actionIdx == curActionIndex && _data.itemValue.Meta > 0) || (_playerActions.Secondary.IsPressed && curAction is ItemActionZoom)) && (rangedData.isReloading || rangedData.isWeaponReloading) && !dataModule.Instance.isInterruptRequested)
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
        if (_actionData?.invData?.holdingEntity is EntityPlayerLocal && AnimationRiggingManager.IsHoldingRiggedWeapon(_actionData.invData.holdingEntity as EntityPlayerLocal))
        {
            return false;
        }
        return true;
    }
}