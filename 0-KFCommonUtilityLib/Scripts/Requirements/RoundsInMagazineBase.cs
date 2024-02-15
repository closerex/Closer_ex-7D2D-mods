using System.Xml.Linq;

public class RoundsInMagazineBase : RoundsInMagazine
{
    protected bool roundsBeforeShot = false;

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        if (base.ParseXAttribute(_attribute))
            return true;

        if (_attribute.Name.LocalName == "rounds_before_shot")
        {
            roundsBeforeShot = bool.Parse(_attribute.Value);
            return true;
        }

        return false;
    }
}

