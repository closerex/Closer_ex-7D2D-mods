using KFCommonUtilityLib;

public class HoldingActionIndexIs : ActionIndexIs
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
        {
            return false;
        }
        return (MultiActionManager.GetActionIndexForEntity(_params.Self) == index) ^ invert;
    }
}
