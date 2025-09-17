using System.Collections.Generic;
using UnityEngine.Scripting;

[Preserve]
public class ItemClassExtendedFunction : ItemClass
{
    public virtual void CancelAllActions(ItemInventoryData _invData)
    {
        for (int i = 0; i < this.Actions.Length; i++)
        {
            if (Actions[i] != null && Actions[i].IsActionRunning(_invData.actionData[i]))
            {
                //Log.Out($"interrupt action on item {Actions[i].item.GetLocalizedItemName()} action{i}");
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
}