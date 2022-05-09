class ItemActionRechargeable : ItemActionAltMode
{
    protected bool altInfiniteAmmo = false;
    protected bool originalInfiniteAmmo = false;
    protected string cvarToConsume = null;
    protected string cvarConsumption = null;
    protected string cvarNoConsumptionTemp = null;

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);

        _props.ParseBool("Alt_InfiniteAmmo", ref altInfiniteAmmo);
        originalInfiniteAmmo = InfiniteAmmo;
        _props.ParseString("Cvar_To_Consume", ref cvarToConsume);
        _props.ParseString("Cvar_Consumption", ref cvarConsumption);
        _props.ParseString("Cvar_No_Consumption_Burst_Count", ref cvarNoConsumptionTemp);
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
        EntityAlive holdingEntity = rangedData.invData.holdingEntity;
        ItemValue value = rangedData.invData.itemValue;
        if (!_bReleased)
        {
            bool isCurAlt = isAltMode(holdingEntity);

            if (isCurAlt && altInfiniteAmmo)
                InfiniteAmmo = true;
            else
                InfiniteAmmo = originalInfiniteAmmo;

            if ((!((int)rangedData.curBurstCount < GetBurstCount(_actionData) || GetBurstCount(_actionData) == -1) || (!InfiniteAmmo && value.Meta <= 0)))
                goto exec;

            if (isCurAlt)
            {
                float consumption = holdingEntity.GetCVar(cvarConsumption);
                if (!string.IsNullOrEmpty(cvarNoConsumptionTemp))
                {
                    float isConsumption0 = holdingEntity.GetCVar(cvarNoConsumptionTemp);
                    if(isConsumption0 > 0)
                    {
                        consumption = 0;
                        holdingEntity.SetCVar(cvarNoConsumptionTemp, --isConsumption0);
                    }
                }

                float stock = holdingEntity.GetCVar(cvarToConsume);
                if (stock < consumption)
                {
                    holdingEntity.PlayOneShot(altSoundEmpty);
                    return;
                }

                if (holdingEntity.inventory.holdingItemItemValue.PercentUsesLeft > 0f)
                {
                    float left = stock - consumption;
                    holdingEntity.SetCVar(cvarToConsume, left);
                }
                base.ExecuteAction(_actionData, _bReleased);
                return;
            }
        }
        else
            InfiniteAmmo = originalInfiniteAmmo;
        exec:
        base.ExecuteAction(_actionData, _bReleased);
    }
}

