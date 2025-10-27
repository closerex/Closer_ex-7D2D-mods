using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(InterruptData))]
public class ActionModuleInterruptReload
{
    public float holdBeforeCancel = 0.06f;
    public string firingStateName = "";
    public bool instantFiringCancel = false;
    public bool internalCancelOnly = false;

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPrefix]
    public bool Prefix_StartHolding(InterruptData __customData)
    {
        __customData.Reset();
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(DynamicProperties _props)
    {
        firingStateName = _props.GetString("FiringStateFullName");
        instantFiringCancel = _props.GetBool("InstantFiringCancel");
        internalCancelOnly = _props.GetBool("InternalCancelOnly");
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemActionData _data, InterruptData __customData)
    {
        var invData = _data.invData;
        __customData.itemAnimator = AnimationGraphBuilder.DummyWrapper;
        __customData.eventBridge = null;
        if (invData.model && invData.model.TryGetComponent<AnimationTargetsAbs>(out var targets) && targets.IsAnimationSet)
        {
            __customData.itemAnimator = targets.GraphBuilder.WeaponWrapper;
            if (__customData.itemAnimator.IsValid)
            {
                __customData.eventBridge = targets.ItemAnimator.GetComponent<AnimationReloadEvents>();
            }
        }
    }

    public struct State
    {
        public bool executed;
        public bool isReloading;
        public bool isWeaponReloading;
        public float lastShotTime;
    }

    [HarmonyPatch(nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    public void Postfix_IsActionRunning(ref bool __result, InterruptData __customData)
    {
        __result &= !__customData.instantFiringRequested;
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPrefix]
    public bool Prefix_ExecuteAction(ItemActionData _actionData, bool _bReleased, InterruptData __customData, out State __state)
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
    public void Postfix_ExecuteAction(ItemActionData _actionData, InterruptData __customData, State __state)
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
                    //__customData.itemAnimator.Update(0f);
                    //__customData.eventBridge.GetComponent<AnimationDelayRender>()?.SkipNextUpdate();
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
    public bool Prefix_ItemActionEffects(ItemActionData _actionData, int _firingState, InterruptData __customData)
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
            if (rangedData != null && rangedData is IModuleContainerFor<ActionModuleInterruptReload.InterruptData> dataModule && rangedAction is IModuleContainerFor<ActionModuleInterruptReload> actionModule && !actionModule.Instance.internalCancelOnly)
            {
                if (!_bReleased && _playerActions != null && actionModule.Instance.IsRequestPossible(dataModule.Instance) && (rangedData.isReloading || rangedData.isWeaponReloading) && !dataModule.Instance.isInterruptRequested)
                {
                    bool isActionInversed = rangedAction is IModuleContainerFor<ActionModuleInversedAction>;
                    bool currentActionPressed = isActionInversed ? _playerActions.Secondary.IsPressed : _playerActions.Primary.IsPressed;
                    bool zoomActionPressed = isActionInversed ? _playerActions.Primary.IsPressed : _playerActions.Secondary.IsPressed;
                    if ((currentActionPressed && _actionIdx == curActionIndex && _data.itemValue.Meta > 0) || (zoomActionPressed && curAction is ItemActionZoom))
                    {
                        if (dataModule.Instance.holdStartTime < 0)
                        {
                            dataModule.Instance.holdStartTime = Time.time;
                            return false;
                        }
                        if (Time.time - dataModule.Instance.holdStartTime >= actionModule.Instance.holdBeforeCancel)
                        {
                            if (!ItemActionRanged.ReloadCancelled(rangedData))
                            {
                                rangedAction.CancelReload(rangedData, false);
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

    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMoveController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_isrunning = AccessTools.Method(typeof(ItemAction), nameof(ItemAction.IsActionRunning));
        int localIndex;
        if (Constants.cVersionInformation.Major == 2 && Constants.cVersionInformation.Minor <= 1)
        {
            localIndex = 38;
        }
        else
        {
            localIndex = 40;
        }
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == localIndex)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (codes[j].Calls(mtd_isrunning))
                    {
                        codes.InsertRange(i - 2, new[]
                        {
                            new CodeInstruction(OpCodes.Brfalse_S, codes[i - 1].labels[0]),
                            new CodeInstruction(OpCodes.Ldarg_0),
                            CodeInstruction.LoadField(typeof(PlayerMoveController), nameof(PlayerMoveController.entityPlayerLocal)),
                            new CodeInstruction(codes[j - 2].opcode, codes[j - 2].operand),
                            CodeInstruction.CallClosure<Func<EntityPlayerLocal, int, bool>>(static (player, actionIndex) =>
                            {
                                return !(player.inventory.holdingItem.Actions[actionIndex] is IModuleContainerFor<ActionModuleInterruptReload> module) || module.Instance.internalCancelOnly;
                            })
                        });
                        break;
                    }
                }
                break;
            }
        }
        return codes;
    }
}