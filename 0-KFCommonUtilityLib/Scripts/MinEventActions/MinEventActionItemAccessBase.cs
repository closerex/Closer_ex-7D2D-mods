using System.Xml.Linq;

public class MinEventActionItemAccessBase : MinEventActionItemCountRandomBase
{
    protected string itemName;
    protected ItemValue itemValueCache = null;

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
            return true;

        switch (_attribute.Name.LocalName)
        {
            case "item":
                itemName = _attribute.Value;
                return true;
            default:
                return false;
        }
    }
}

