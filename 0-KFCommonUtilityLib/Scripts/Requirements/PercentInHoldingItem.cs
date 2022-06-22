using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class PercentInHoldingItem : RoundsInHoldingItem
{
	public override bool IsValid(MinEventParams _params)
	{
		if (!ParamsValid(_params))
			return false;

		ItemValue holdingItemValue = _params.Self.inventory.holdingItemItemValue;
		if (holdingItemValue.IsEmpty() || !(holdingItemValue.ItemClass.Actions[0] is ItemActionRanged _ranged))
			return false;

		return RequirementBase.compareValues((float)holdingItemValue.Meta / _ranged.GetMaxAmmoCount(_params.Self.inventory.holdingItemData.actionData[0]), operation, value) ^ invert;
	}
}

