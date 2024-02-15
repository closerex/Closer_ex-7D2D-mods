using KFCommonUtilityLib.Scripts.StaticManagers;

public class HoldingActionIndexIs : ActionIndexIs
{
    public override bool IsValid(MinEventParams _params)
    {
        return (MultiActionManager.GetActionIndexForEntity(_params.Self) == index) ^ invert;
    }
}
