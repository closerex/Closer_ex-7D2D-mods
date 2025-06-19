using System.Xml.Linq;

public class ItemInInventory : RequirementBase
{
    private string itemName;
    private ItemValue itemValueCache = null;

    public override bool IsValid(MinEventParams _params)
    {
        return base.IsValid(_params) && (itemValueCache != null || (itemValueCache = ItemClass.GetItem(itemName)) != null) && compareValues(_params.Self.GetItemCount(itemValueCache), operation, value) ^ invert;
    }

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        if (base.ParseXAttribute(_attribute))
            return true;

        string name = _attribute.Name.LocalName;
        if (name == "item")
        {
            itemName = _attribute.Value;
            return true;
        }
        return false;
    }
}
