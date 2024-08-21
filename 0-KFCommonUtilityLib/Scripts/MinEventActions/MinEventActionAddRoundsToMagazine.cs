using System.Xml.Linq;

public class MinEventActionAddRoundsToMagazine : MinEventActionAmmoAccessBase
{
    private float maxPerc = -1;
    public override void Execute(MinEventParams _params)
    {
        _params.ItemValue.Meta += GetCount(_params);
        if (maxPerc > 0)
            _params.ItemValue.Meta = Utils.FastMin((int)((_params.ItemValue.ItemClass.Actions[0] as ItemActionRanged).GetMaxAmmoCount(_params.ItemActionData) * maxPerc), _params.ItemValue.Meta);
        _params.Self?.inventory?.CallOnToolbeltChangedInternal();
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
            return true;

        if (_attribute.Name.LocalName == "max")
        {
            maxPerc = float.Parse(_attribute.Value);
            return true;
        }

        return false;
    }
}

