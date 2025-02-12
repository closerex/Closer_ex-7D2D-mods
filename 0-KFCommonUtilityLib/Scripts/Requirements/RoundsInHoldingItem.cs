using KFCommonUtilityLib.Scripts.StaticManagers;

public class RoundsInHoldingItem : RoundsInMagazineBase
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!ParamsValid(_params))
            return false;

        ItemValue holdingItemValue = _params.Self.inventory.holdingItemItemValue;
        if (holdingItemValue.IsEmpty() || !(holdingItemValue.ItemClass.Actions[MultiActionManager.GetActionIndexForEntity(_params.Self)] is ItemActionRanged))
            return false;

        return RequirementBase.compareValues((float)(roundsBeforeShot ? holdingItemValue.Meta + 1 : holdingItemValue.Meta), operation, value) ^ invert;
    }
}

