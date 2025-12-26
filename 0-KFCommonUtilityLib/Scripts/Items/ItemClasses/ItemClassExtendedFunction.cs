using HarmonyLib;
using KFCommonUtilityLib;
using System.Collections.Generic;
using UnityEngine.Scripting;

[Preserve]
public class ItemClassExtendedFunction : ItemClass, ILateInitItem
{
    public virtual void CancelAllActions(ItemInventoryData _invData)
    {
        for (int i = 0; i < this.Actions.Length; i++)
        {
            if (Actions[i] != null && Actions[i].IsActionRunning(_invData.actionData[i]))
            {
                //Log.Out($"interrupt action on item {Actions[i].item.GetLocalizedItemName()} action{i}");
                if (_invData.actionData[i] is IModuleContainerFor<ActionModuleAnimationInterruptable.AnimationInterruptableData> interruptData)
                {
                    interruptData.Instance.interruptRequested = true;
                }
                Actions[i].CancelAction(_invData.actionData[i]);
            }
        }
    }

    public virtual void GetAllExecutingActions(ItemInventoryData _invData, List<ItemAction> _actionList, List<ItemActionData> _dataList)
    {
        for (int i = 0; i < this.Actions.Length; i++)
        {
            if (Actions[i] != null && Actions[i].IsActionRunning(_invData.actionData[i]))
            {
                _actionList.Add(Actions[i]);
                _dataList.Add(_invData.actionData[i]);
            }
        }
    }

    public virtual void LateInitItem()
    {

    }

    public virtual void OnToggleItemActivation(ItemInventoryData _data)
    {

    }
}

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class ItemClassExtendedFunctionPatches
    {
        [HarmonyPatch(typeof(XUiC_Radial), nameof(XUiC_Radial.handleActivatableItemCommand))]
        [HarmonyPostfix]
        public static void Postfix_handleActivatableItemCommand_XUiC_Radial(XUiC_Radial __instance, int _commandIndex, XUiC_Radial _sender)
        {
            EntityPlayerLocal player = _sender.xui.playerUI.entityPlayer;
            var activateValue = __instance.activatableItemPool[_commandIndex];
            var activateItem = activateValue.ItemClass;
            if (activateItem is ItemClassExtendedFunction itemExtFunc)
            {
                if (activateValue == player.inventory.holdingItemItemValue)
                {
                    itemExtFunc.OnToggleItemActivation(player.inventory.holdingItemData);
                }
            }
        }
    }
}
