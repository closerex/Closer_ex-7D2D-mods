public class MinEventActionRemoveRoundsFromInventory : MinEventActionAmmoAccessBase
{
    private ItemValue itemValueCache;

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        if (!base.CanExecute(_eventType, _params))
            return false;

        var _ranged = _params.ItemValue.ItemClass.Actions[_params.ItemActionData.indexInEntityOfAction] as ItemActionRanged;
        string ammoName = _ranged.MagazineItemNames[_params.ItemValue.SelectedAmmoTypeIndex];
        return RoundsInInventory.TryGetValue(ammoName, out itemValueCache);
    }
    public override void Execute(MinEventParams _params)
    {
        _params.Self.TryRemoveItem(GetCount(_params), itemValueCache);
    }
}

