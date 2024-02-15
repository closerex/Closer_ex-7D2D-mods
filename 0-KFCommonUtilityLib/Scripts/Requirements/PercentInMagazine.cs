public class PercentInMagazine : RoundsInMagazineBase
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!ParamsValid(_params))
            return false;

        if (_params.ItemValue.IsEmpty() || !(_params.ItemValue.ItemClass.Actions[0] is ItemActionRanged _ranged) || _params.ItemActionData.invData == null)
            return false;

        return RequirementBase.compareValues((float)(roundsBeforeShot ? _params.ItemValue.Meta + 1 : _params.ItemValue.Meta) / _ranged.GetMaxAmmoCount(_params.ItemActionData), operation, value) ^ invert;
    }
}

