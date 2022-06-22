using System.Xml;

public class MinEventActionAddRoundsToMagazine : MinEventActionAmmoAccessBase
{
    private float maxPerc = -1;
    public override void Execute(MinEventParams _params)
    {
        _params.ItemValue.Meta += GetCount(_params);
        if(maxPerc > 0)
            _params.ItemValue.Meta = Utils.FastMin((int)((_params.ItemValue.ItemClass.Actions[0] as ItemActionRanged).GetMaxAmmoCount(_params.ItemActionData) * maxPerc), _params.ItemValue.Meta);
    }

    public override bool ParseXmlAttribute(XmlAttribute _attribute)
    {
        if(base.ParseXmlAttribute(_attribute))
            return true;

        if(_attribute.Name == "max")
        {
            maxPerc = float.Parse(_attribute.Value);
            return true;
        }

        return false;
    }
}

