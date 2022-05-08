using UnityEngine;

class ItemActionRechargeable : ItemActionRanged
{
    private string cvarStateSwitch = null;
    private string cvarToConsume = null;
    private string cvarConsumption = null;
    private string cvarNoConsumptionOnHit = null;
    private string originalSoundStart = string.Empty;
    private string originalSoundLoop = string.Empty;
    private string originalSoundEnd = string.Empty;
    private string originalSoundEmpty = string.Empty;
    private string altSoundStart = string.Empty;
    private string altSoundLoop = string.Empty;
    private string altSoundEnd = string.Empty;
    private string altSoundEmpty = string.Empty;
    private string altModeAnimatorBool = "altMode";
    private string emptyAnimatorBool = "empty";
    private bool altInfiniteAmmo = false;
    private bool originalInfiniteAmmo = false;

    public bool isAltMode(EntityAlive holdingEntity)
    {
        return !string.IsNullOrEmpty(cvarStateSwitch) && holdingEntity && holdingEntity.GetCVar(cvarStateSwitch) > 0;
    }

    public void setAnimatorBool(EntityAlive holdingEntity, string parameter, bool flag)
    {
        Transform trans = (holdingEntity.emodel.avatarController as AvatarMultiBodyController)?.HeldItemTransform;
        if (trans && trans.TryGetComponent<Animator>(out Animator animator))
        {
            animator.SetBool(parameter, flag);
            Log.Out("trying to set param: " + parameter + " flag: " + flag + " result: " + getAnimatorBool(holdingEntity, parameter) + " transform: " + animator.transform.name);
        }
    }

    public bool getAnimatorBool(EntityAlive holdingEntity, string parameter)
    {
        Transform trans = (holdingEntity.emodel.avatarController as AvatarMultiBodyController)?.HeldItemTransform;
        if (trans && trans.TryGetComponent<Animator>(out Animator animator))
            return animator.GetBool(parameter);
        else
            return false;
    }

    private void setAltSound(bool isAlt, ItemActionData _actionData)
    {
        ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
        if (isAlt)
        {
            rangedData.SoundStart = altSoundStart;
            rangedData.SoundLoop = altSoundLoop;
            rangedData.SoundEnd = altSoundEnd;
            soundEmpty = altSoundEmpty;
        }else
        {
            rangedData.SoundStart = originalSoundStart;
            rangedData.SoundLoop = originalSoundLoop;
            rangedData.SoundEnd = originalSoundEnd;
            soundEmpty = originalSoundEmpty;
        }

        if (rangedData.SoundStart.Contains("silenced"))
            rangedData.IsFlashSuppressed = true;
    }

    private void setAltSound(ItemActionData _actionData)
    {
        setAltSound(isAltMode(_actionData.invData.holdingEntity), _actionData);
    }

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);

        _props.ParseString("Cvar_State_Switch", ref cvarStateSwitch);
        _props.ParseString("Cvar_To_Consume", ref cvarToConsume);
        _props.ParseString("Cvar_Consumption", ref cvarConsumption);
        _props.ParseString("Cvar_No_Consumption_On_Hit", ref cvarNoConsumptionOnHit);
        _props.ParseString("Sound_start", ref originalSoundStart);
        _props.ParseString("Sound_loop", ref originalSoundLoop);
        _props.ParseString("Sound_end", ref originalSoundEnd);
        _props.ParseString("Sound_empty", ref originalSoundEmpty);
        _props.ParseString("Alt_Sound_Start", ref altSoundStart);
        if (string.IsNullOrEmpty(altSoundStart))
            altSoundStart = originalSoundStart;
        _props.ParseString("Alt_Sound_Loop", ref altSoundLoop);
        if (string.IsNullOrEmpty(altSoundLoop))
            altSoundLoop = originalSoundLoop;
        _props.ParseString("Alt_Sound_End", ref altSoundEnd);
        if (string.IsNullOrEmpty(altSoundEnd))
            altSoundEnd = originalSoundEnd;
        _props.ParseString("Alt_Sound_Empty", ref altSoundEmpty);
        if (string.IsNullOrEmpty(altSoundEmpty))
            altSoundEmpty = soundEmpty;
        _props.ParseBool("Alt_InfiniteAmmo", ref altInfiniteAmmo);
        originalInfiniteAmmo = InfiniteAmmo;
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

            if (!InfiniteAmmo && value.Meta <= 1)
                setAnimatorBool(holdingEntity, emptyAnimatorBool, true);
            if (isCurAlt)
            {
                float consumption = holdingEntity.GetCVar(cvarConsumption);
                if (!string.IsNullOrEmpty(cvarNoConsumptionOnHit))
                {
                    float isConsumption0 = holdingEntity.GetCVar(cvarNoConsumptionOnHit);
                    if(isConsumption0 > 0)
                    {
                        consumption = 0;
                        holdingEntity.SetCVar(cvarNoConsumptionOnHit, --isConsumption0);
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
    public override void ItemActionEffects(GameManager _gameManager, ItemActionData _actionData, int _firingState, Vector3 _startPos, Vector3 _direction, int _userData = 0)
    {
        setAltSound(_actionData);
        base.ItemActionEffects(_gameManager, _actionData, _firingState, _startPos, _direction, _userData);
    }
    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);

        if (GameManager.IsDedicatedServer)
            return;

        EntityAlive holdingEntity = _actionData.invData.holdingEntity;
        int id = holdingEntity.entityId;

        bool isCurAlt = isAltMode(holdingEntity);
        if(isCurAlt != getAnimatorBool(holdingEntity, altModeAnimatorBool))
        {
            setAnimatorBool(holdingEntity, altModeAnimatorBool, isCurAlt);
        }

        int meta = _actionData.invData.itemValue.Meta;
        bool isReloading = (_actionData as ItemActionDataRanged).isReloading;
        if (!isReloading && meta <= 0 && !getAnimatorBool(holdingEntity, emptyAnimatorBool))
        {
            Log.Out("trying to update param: " + emptyAnimatorBool + " flag: " + true);
            setAnimatorBool(holdingEntity, emptyAnimatorBool, true);
        }else if ((isReloading || meta > 0) && getAnimatorBool(holdingEntity, emptyAnimatorBool))
        {
            Log.Out("trying to update param: " + emptyAnimatorBool + " flag: " + false);
            setAnimatorBool(holdingEntity, emptyAnimatorBool, false);
        }
    }
}

