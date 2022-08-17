using System.Xml;

public class RoundsInMagazineBase : RoundsInMagazine
{
    protected bool roundsBeforeShot = false;

    public override bool ParseXmlAttribute(XmlAttribute _attribute)
    {
        if(base.ParseXmlAttribute(_attribute))
            return true;

        if(_attribute.Name == "rounds_before_shot")
        {
            roundsBeforeShot = bool.Parse(_attribute.Value);
            return true;
        }

        return false;
    }
}

