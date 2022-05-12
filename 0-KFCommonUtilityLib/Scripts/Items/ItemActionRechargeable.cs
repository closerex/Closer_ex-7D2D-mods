class ItemActionRechargeable : ItemActionAltMode
{
    protected string[] cvarToConsume = null;
    protected string[] cvarConsumption = null;
    protected string[] cvarNoConsumptionTemp = null;

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);

        string _altString = string.Empty;
        _props.ParseString("Cvar_To_Consume", ref _altString);
        cvarToConsume = _altString.Split(',');
        _altString = string.Empty;
        _props.ParseString("Cvar_Consumption", ref _altString);
        cvarConsumption = _altString.Split(',');
        if (cvarToConsume.Length != cvarConsumption.Length)
            Log.Error("cvar to consume count does not match cvar consumption count!");
        _altString = string.Empty;
        _props.ParseString("Cvar_No_Consumption_Burst_Count", ref _altString);
        cvarNoConsumptionTemp = _altString.Split(',');
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        ItemActionDataAltMode _data = _actionData as ItemActionDataAltMode;
        EntityAlive holdingEntity = _data.invData.holdingEntity;
        ItemValue value = _data.invData.itemValue;
        if (!_bReleased)
        {
            int curAltIndex = _data.modeIndex;
            if (curAltIndex >= 0)
                InfiniteAmmo = altInfiniteAmmo.Length > curAltIndex ? altInfiniteAmmo[curAltIndex] : false;
            else
                InfiniteAmmo = originalInfiniteAmmo;
            if ((!((int)_data.curBurstCount < GetBurstCount(_actionData) || GetBurstCount(_actionData) == -1) || (!InfiniteAmmo && value.Meta <= 0)))
            {
                base.ExecuteAction(_actionData, _bReleased);
                return;
            }

            if (curAltIndex >= 0 && cvarConsumption.Length > curAltIndex && !string.IsNullOrEmpty(cvarConsumption[curAltIndex]))
            {
                float consumption = holdingEntity.GetCVar(cvarConsumption[curAltIndex]);
                if (cvarNoConsumptionTemp.Length > curAltIndex && !string.IsNullOrEmpty(cvarNoConsumptionTemp[curAltIndex]))
                {
                    float isConsumption0 = holdingEntity.GetCVar(cvarNoConsumptionTemp[curAltIndex]);
                    if(isConsumption0 > 0)
                    {
                        consumption = 0;
                        holdingEntity.SetCVar(cvarNoConsumptionTemp[curAltIndex], --isConsumption0);
                    }
                }

                float stock = holdingEntity.GetCVar(cvarToConsume[curAltIndex]);
                if (stock < consumption)
                {
                    holdingEntity.PlayOneShot(altSoundEmpty.Length >= curAltIndex ? altSoundEmpty[curAltIndex] : originalSoundEmpty);
                    return;
                }

                if (holdingEntity.inventory.holdingItemItemValue.PercentUsesLeft > 0f)
                {
                    float left = stock - consumption;
                    holdingEntity.SetCVar(cvarToConsume[curAltIndex], left);
                }
                base.ExecuteAction(_actionData, _bReleased);
                return;
            }
        }
        base.ExecuteAction(_actionData, _bReleased);
    }
}

