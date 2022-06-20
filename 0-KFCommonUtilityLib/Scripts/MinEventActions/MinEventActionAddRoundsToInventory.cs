public class MinEventActionAddRoundsToInventory : MinEventActionAmmoAccessBase
{
    private ItemStack itemStackCache = new ItemStack();

    public override void Execute(MinEventParams _params)
    {
        var _ranged = _params.ItemValue.ItemClass.Actions[0] as ItemActionRanged;
        string ammoName = _ranged.MagazineItemNames[_params.ItemValue.SelectedAmmoTypeIndex];
        if(!RoundsInInventory.TryGetValue(ammoName, out var ammoValue))
            return;
        itemStackCache.itemValue = ammoValue;
        itemStackCache.count = GetCount(_params);
        _params.Self.TryStackItem(itemStackCache);
    }
}

