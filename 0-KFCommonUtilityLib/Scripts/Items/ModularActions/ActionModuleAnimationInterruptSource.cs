﻿using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(AnimationInterruptSourceData))]
public class ActionModuleAnimationInterruptSource
{
    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(AnimationInterruptSourceData __customData)
    {
        __customData.interrupted = false;
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(AnimationInterruptSourceData __customData)
    {
        __customData.interrupted = false;
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPostfix]
    public void Postfix_ExecuteAction(AnimationInterruptSourceData __customData, bool _bReleased)
    {
        __customData.interrupted = !_bReleased;
    }

    public static void GetAllRunningAndInterruptableActions(EntityPlayerLocal player, ItemAction interruptAction, ItemActionData interruptData, List<ItemAction> actionList, List<ItemActionData> dataList, out bool isActionRunning, out bool isAllRunningActionInterruptable)
    {
        var itemExtFunc = player.inventory.holdingItem as ItemClassExtendedFunction;
        if (itemExtFunc != null)
        {
            itemExtFunc.GetAllExecutingActions(player.inventory.holdingItemData, actionList, dataList);
            isActionRunning = actionList.Count > 0;
            isAllRunningActionInterruptable = interruptAction is IModuleContainerFor<ActionModuleAnimationInterruptSource> && !interruptAction.IsActionRunning(interruptData) && interruptData is IModuleContainerFor<AnimationInterruptSourceData> dataModule && !dataModule.Instance.interrupted;
            if (isAllRunningActionInterruptable)
            {
                for (int i = 0; i < actionList.Count; i++)
                {
                    if (actionList[i] is not IModuleContainerFor<ActionModuleAnimationInterruptable> animationModule || dataList[i] is not IModuleContainerFor<ActionModuleAnimationInterruptable.AnimationInterruptableData> animationModuleData)
                    {
                        isAllRunningActionInterruptable = false;
                        break;
                    }

                    isAllRunningActionInterruptable = animationModuleData.Instance.IsInterruptable();
                    if (!isAllRunningActionInterruptable)
                    {
                        break;
                    }
                }
            }
        }
        else
        {
            isActionRunning = player.inventory.IsHoldingItemActionRunning();
            isAllRunningActionInterruptable = false;
        }
    }

    public class AnimationInterruptSourceData
    {
        public List<ItemAction> cachedActionList = new List<ItemAction>();
        public List<ItemActionData> cachedDataList = new List<ItemActionData>();
        public bool interrupted = false;
    }
}

[HarmonyPatch]
public static class ZAnimationInterruptSourcePatches
{

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction))]
    [HarmonyPrefix]
    private static bool Prefix_ExecuteAction_ItemClass(ItemClass __instance, int _actionIdx, ItemInventoryData _data, bool _bReleased, PlayerActionsLocal _playerActions)
    {
        ItemAction curAction = __instance.Actions[_actionIdx];
        ItemActionData curActionData = _data.actionData[_actionIdx];
        if (!_bReleased && _playerActions != null)
        {
            if (curAction is IModuleContainerFor<ActionModuleAnimationInterruptSource> module && curActionData is IModuleContainerFor<ActionModuleAnimationInterruptSource.AnimationInterruptSourceData> dataModule)
            {
                ActionModuleAnimationInterruptSource.AnimationInterruptSourceData interruptData = dataModule.Instance;
                interruptData.cachedActionList.Clear();
                interruptData.cachedDataList.Clear();
                ActionModuleAnimationInterruptSource.GetAllRunningAndInterruptableActions(_data.holdingEntity as EntityPlayerLocal, curAction, curActionData, interruptData.cachedActionList, interruptData.cachedDataList, out bool isActionRunning, out bool isAllRunningActionInterruptable);
                //Log.Out($"isActionRunning {isActionRunning} isAllRunningActionInterruptable {isAllRunningActionInterruptable} running actions {string.Join(' ', interruptData.cachedActionList.Select(static action => $"{action.item.GetLocalizedItemName()}.action{action.ActionIndex}"))}");

                if (isActionRunning)
                {
                    if (isAllRunningActionInterruptable && !interruptData.interrupted)
                    {
                        //Log.Out($"Cancel action on item {__instance.GetLocalizedItemName()} by action {_actionIdx}");
                        (__instance as ItemClassExtendedFunction)?.CancelAllActions(_data);
                        interruptData.interrupted = true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }
        else if (_bReleased)
        {
            if (curActionData is IModuleContainerFor<ActionModuleAnimationInterruptSource.AnimationInterruptSourceData> dataModule)
            {
                dataModule.Instance.interrupted = false;
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMovementController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_isrunning = AccessTools.Method(typeof(ItemAction), nameof(ItemAction.IsActionRunning));
        int localIndexAction0, localIndexAction1;
        if (Constants.cVersionInformation.Major == 2 && Constants.cVersionInformation.Minor <= 1)
        {
            localIndexAction0 = 38;
            localIndexAction1 = 37;
        }
        else
        {
            localIndexAction0 = 40;
            localIndexAction1 = 39;
        }

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_S && (((LocalBuilder)codes[i].operand).LocalIndex == localIndexAction0 || ((LocalBuilder)codes[i].operand).LocalIndex == localIndexAction1))
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
                                return player.inventory.holdingItem.Actions[actionIndex] is not IModuleContainerFor<ActionModuleAnimationInterruptSource> module;
                            })
                        });
                        i += 5;
                        break;
                    }
                }
            }
        }

        return codes;
    }
}