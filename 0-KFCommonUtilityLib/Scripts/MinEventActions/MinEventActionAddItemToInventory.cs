public class MinEventActionAddItemToInventory : MinEventActionItemAccessBase
{
    private ItemStack itemStackCache = new ItemStack();

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        if (itemValueCache == null)
        {
            itemValueCache = ItemClass.GetItem(itemName);
            itemStackCache.itemValue = itemValueCache;
        }
        return !_params.Self.isEntityRemote && base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        itemStackCache.count = GetCount(_params);
        _params.Self.TryStackItem(itemStackCache);
    }
}

