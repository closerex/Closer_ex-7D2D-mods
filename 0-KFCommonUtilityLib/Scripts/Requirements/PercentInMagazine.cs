public class PercentInMagazine : RoundsInMagazine
{
	public override bool IsValid(MinEventParams _params)
	{
		if (!ParamsValid(_params))
			return false;

		if (_params.ItemValue.IsEmpty() || !(_params.ItemValue.ItemClass.Actions[0] is ItemActionRanged _ranged))
			return false;

		return RequirementBase.compareValues((float)_params.ItemValue.Meta / _ranged.GetMaxAmmoCount(_params.ItemActionData), operation, value) ^ invert;
	}
}

