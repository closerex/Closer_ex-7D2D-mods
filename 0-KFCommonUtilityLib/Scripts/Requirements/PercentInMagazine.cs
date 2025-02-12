public class PercentInMagazine : RoundsInMagazineBase
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!ParamsValid(_params))
            return false;

        if (_params.ItemValue.IsEmpty() || !(_params.ItemActionData is ItemActionRanged.ItemActionDataRanged _rangedData) || _params.ItemActionData.invData == null)
            return false;

        return RequirementBase.compareValues((float)(roundsBeforeShot ? _params.ItemValue.Meta + 1 : _params.ItemValue.Meta) / ((ItemActionRanged)_params.ItemValue.ItemClass.Actions[_rangedData.indexInEntityOfAction]).GetMaxAmmoCount(_params.ItemActionData), operation, value) ^ invert;
    }
}

