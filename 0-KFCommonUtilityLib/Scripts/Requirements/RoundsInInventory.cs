using System.Collections.Generic;

public class RoundsInInventory : RequirementBase
{
    private static Dictionary<string, ItemValue> cached = new Dictionary<string, ItemValue>();

    public static bool TryGetValue(string ammoName, out ItemValue ammoValue)
    {
        if (!cached.TryGetValue(ammoName, out ammoValue))
        {
            ammoValue = ItemClass.GetItem(ammoName, false);
            if (ammoValue == null)
                return false;
            cached.Add(ammoName, ammoValue);
        }
        return true;
    }
    public override bool IsValid(MinEventParams _params)
    {
        if (!this.ParamsValid(_params))
            return false;

        ItemValue itemValue = _params.ItemValue;
        if (itemValue.IsEmpty() || !(itemValue.ItemClass.Actions[0] is ItemActionRanged _ranged))
            return false;

        string ammoName = _ranged.MagazineItemNames[itemValue.SelectedAmmoTypeIndex];
        if (TryGetValue(ammoName, out var ammoValue))
            return compareValues(_params.Self.GetItemCount(ammoValue), this.operation, this.value) ^ invert;
        return false;
    }
}
