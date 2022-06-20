using System.Xml;
using UnityEngine;

public class MinEventActionItemAccessBase : MinEventActionItemCountRandomBase
{
    protected string itemName;
    protected ItemValue itemValueCache = null;

    public override bool ParseXmlAttribute(XmlAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
            return true;

        switch (_attribute.Name)
        {
            case "item":
                itemName = _attribute.Value;
                return true;
            default:
                return false;
        }
    }
}

