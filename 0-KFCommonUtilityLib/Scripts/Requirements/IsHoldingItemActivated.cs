public class IsHoldingItemActivated : RequirementBase
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
        {
            return false;
        }
        ItemValue itemValue = _params.Self?.inventory?.holdingItemItemValue;
        if (itemValue == null)
        {
            return false;
        }

        return invert ^ itemValue.Activated > 0;
    }
}
