using KFCommonUtilityLib;

public class RoundsInHoldingItem : RoundsInMagazineBase
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
        if (holdingItemValue.IsEmpty() || !(holdingItemValue.ItemClass.Actions[MultiActionManager.GetActionIndexForEntity(_params.Self)] is ItemActionRanged))
            return false;

        return RequirementBase.compareValues((float)(roundsBeforeShot ? holdingItemValue.Meta + 1 : holdingItemValue.Meta), operation, value) ^ invert;
    }
}

