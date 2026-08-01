using KFCommonUtilityLib;
using System.Xml.Linq;

public class IsActionUnlocked : TargetedCompareRequirementBase
{
    protected int actionIndex;

    public override bool IsValid(MinEventParams _params)
    {
        return base.IsValid(_params) && 
               ((actionIndex == 0 ||
                    ((_params.ItemActionData?.invData?.actionData?[0].IsModuleContainerFor(out ActionModuleAlternative.AlternativeData alt) ?? false)
                        && alt.IsActionUnlocked(actionIndex))) ^ invert);
    }

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        if (base.ParseXAttribute(_attribute))
            return true;

        if (_attribute.Name == "index")
        {
            actionIndex = int.Parse(_attribute.Value);
            return true;
        }
        return false;
    }
}
