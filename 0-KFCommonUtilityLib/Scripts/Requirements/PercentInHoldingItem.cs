using KFCommonUtilityLib;

public class PercentInHoldingItem : RoundsInHoldingItem
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
        {
            return false;
        }
        if (cvarName != null)
        {
            value = _params.Self.Buffs.GetCustomVar(cvarName);
        }

        ItemValue holdingItemValue = _params.Self.inventory.holdingItemItemValue;
        int actionIndex = MultiActionManager.GetActionIndexForEntity(_params.Self);
        if (holdingItemValue.IsEmpty() || !(holdingItemValue.ItemClass.Actions[actionIndex] is ItemActionRanged _ranged))
            return false;
        return RequirementBase.compareValues((float)(roundsBeforeShot ? holdingItemValue.Meta + 1 : holdingItemValue.Meta) / _ranged.GetMaxAmmoCount(_params.Self.inventory.holdingItemData.actionData[actionIndex]), operation, value) ^ invert;
    }
}

