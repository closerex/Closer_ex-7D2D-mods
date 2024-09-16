public class HoldingAmmoIndexIs : AmmoIndexIs
{
    public override bool ParamsValid(MinEventParams _params)
    {
        itemValueCache = _params.Self?.inventory?.holdingItemItemValue;
        return itemValueCache != null;
    }
}