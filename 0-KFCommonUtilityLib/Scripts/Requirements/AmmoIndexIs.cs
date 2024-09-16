using KFCommonUtilityLib.Scripts.Utilities;
using System.Xml.Linq;

public class AmmoIndexIs : RequirementBase
{
    protected int ammoIndex;
    protected int actionIndex;
    protected ItemValue itemValueCache;

    public override bool IsValid(MinEventParams _params)
    {
        bool res = false;
        int parAmmoIndex = itemValueCache.GetSelectedAmmoIndexByActionIndex(actionIndex);
        res = parAmmoIndex == ammoIndex;
        itemValueCache = null;
        if (invert)
        {
            return !res;
        }
        return res;
    }

    public override bool ParamsValid(MinEventParams _params)
    {
        itemValueCache = _params.ItemValue;
        return itemValueCache != null;
    }

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        switch(_attribute.Name.LocalName)
        {
            case "ammoIndex":
                ammoIndex = int.Parse(_attribute.Value);
                break;
            case "actionIndex":
                actionIndex = int.Parse(_attribute.Value);
                break;
            default:
                return false;
        }
        return true;
    }
}