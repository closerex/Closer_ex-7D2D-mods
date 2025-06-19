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
        if (!base.IsValid(_params))
        {
            return false;
        }
        if (cvarName != null)
        {
            value = _params.Self.Buffs.GetCustomVar(cvarName);
        }

        ItemValue itemValue = _params.ItemValue;
        if (itemValue.IsEmpty() || !(_params.ItemActionData is ItemActionRanged.ItemActionDataRanged _rangedData))
            return invert;

        string ammoName = ((ItemActionRanged)itemValue.ItemClass.Actions[_rangedData.indexInEntityOfAction]).MagazineItemNames[itemValue.SelectedAmmoTypeIndex];
        if (TryGetValue(ammoName, out var ammoValue))
            return compareValues(_params.Self.GetItemCount(ammoValue), this.operation, this.value) ^ invert;
        return invert;
    }
}
