public class PercentInHoldingItem : RoundsInHoldingItem
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!ParamsValid(_params))
            return false;

        ItemValue holdingItemValue = _params.Self.inventory.holdingItemItemValue;
        if (holdingItemValue.IsEmpty() || !(holdingItemValue.ItemClass.Actions[0] is ItemActionRanged _ranged))
            return false;

        return RequirementBase.compareValues((float)(roundsBeforeShot ? holdingItemValue.Meta + 1 : holdingItemValue.Meta) / _ranged.GetMaxAmmoCount(_params.Self.inventory.holdingItemData.actionData[0]), operation, value) ^ invert;
    }
}

