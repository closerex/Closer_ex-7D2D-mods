public class HoldingAmmoIndexIs : AmmoIndexIs
{
    public override bool CacheItem(MinEventParams _params)
    {
        itemValueCache = _params.Self?.inventory?.holdingItemItemValue;
        return itemValueCache != null;
    }
}