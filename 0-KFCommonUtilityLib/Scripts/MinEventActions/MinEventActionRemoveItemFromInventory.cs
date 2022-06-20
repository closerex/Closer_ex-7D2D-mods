public class MinEventActionRemoveItemFromInventory : MinEventActionItemAccessBase
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        if (itemValueCache == null)
            itemValueCache = ItemClass.GetItem(itemName);
        return !_params.Self.isEntityRemote && base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        _params.Self.TryRemoveItem(GetCount(_params), itemValueCache);
    }
}

