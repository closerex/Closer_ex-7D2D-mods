using System.Xml.Linq;

public class MinEventActionAmmoAccessBase : MinEventActionItemCountRandomBase
{
    private bool useMag = false;
    private bool useRounds = false;
    private bool revert = false;
    private float perc = 1;
    protected override int GetCount(MinEventParams _params)
    {
        if (!useMag || !(_params.ItemValue.ItemClass.Actions[_params.ItemActionData.indexInEntityOfAction] is ItemActionRanged _ranged))
            return base.GetCount(_params);

        if (!useRounds)
            return (int)(_ranged.GetMaxAmmoCount(_params.ItemActionData) * perc);

        if (!revert)
            return (int)((_params.ItemValue.Meta) * perc);

        return (int)((_ranged.GetMaxAmmoCount(_params.ItemActionData) - _params.ItemValue.Meta) * perc);
    }

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return !_params.Self.isEntityRemote && base.CanExecute(_eventType, _params) && _params.ItemActionData is ItemActionRanged.ItemActionDataRanged && _params.ItemValue.ItemClass.Actions[_params.ItemActionData.indexInEntityOfAction] is ItemActionRanged;
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
            return true;

        if (_attribute.Name.LocalName == "count" && _attribute.Value.Contains("MagazineSize"))
        {
            useMag = true;
            string str = _attribute.Value;
            if (str.StartsWith("%"))
            {
                useRounds = true;
                str = str.Substring(1);
            }

            if (str.StartsWith("!"))
            {
                revert = true;
                str = str.Substring(1);
            }

            string[] arr = str.Split(new char[] { '*' }, 2, System.StringSplitOptions.RemoveEmptyEntries);
            if (arr.Length == 2)
                return float.TryParse(arr[1], out perc);
            return true;
        }
        return false;
    }
}
