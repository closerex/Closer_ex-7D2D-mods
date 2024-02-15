﻿using System.Xml.Linq;

public class ItemInInventory : RequirementBase
{
    private string itemName;
    private ItemValue itemValueCache = null;

    public override bool IsValid(MinEventParams _params)
    {
        return base.IsValid(_params) && compareValues(_params.Self.GetItemCount(itemValueCache), operation, value) ^ invert;
    }

    public override bool ParamsValid(MinEventParams _params)
    {
        if (itemValueCache == null)
            itemValueCache = ItemClass.GetItem(itemName);
        return base.ParamsValid(_params) && itemValueCache != null;
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
