class RoundsInHoldingItem : RoundsInMagazine
{
    public override bool IsValid(MinEventParams _params)
	{
		if (!this.ParamsValid(_params))
		{
			return false;
		}
		ItemValue holdingItemValue = _params.Self.inventory.holdingItemItemValue;
		if (holdingItemValue.IsEmpty() || !(holdingItemValue.ItemClass.Actions[0] is ItemActionRanged))
		{
			return false;
		}
		if (this.invert)
		{
			return !RequirementBase.compareValues((float)holdingItemValue.Meta, this.operation, this.value);
		}
		return RequirementBase.compareValues((float)holdingItemValue.Meta, this.operation, this.value);
	}
}

