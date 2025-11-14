public class IsItemActivated : RequirementBase
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params) || _params.ItemValue == null)
        {
            return false;
        }

        return invert ^ _params.ItemValue.Activated > 0;
    }
}