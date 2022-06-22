public class RoundsInHoldingItem : RoundsInMagazine
{
    public override bool IsValid(MinEventParams _params)
	{
		if (!ParamsValid(_params))
			return false;

		ItemValue holdingItemValue = _params.Self.inventory.holdingItemItemValue;
		if (holdingItemValue.IsEmpty() || !(holdingItemValue.ItemClass.Actions[0] is ItemActionRanged))
			return false;

		return RequirementBase.compareValues((float)holdingItemValue.Meta, operation, value) ^ invert;
	}
}

