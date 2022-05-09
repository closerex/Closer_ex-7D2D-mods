using UnityEngine;

class ItemActionAltMode : ItemActionHoldOpen
{
    protected string cvarStateSwitch = null;
    protected string originalSoundStart = string.Empty;
    protected string originalSoundLoop = string.Empty;
    protected string originalSoundEnd = string.Empty;
    protected string originalSoundEmpty = string.Empty;
    protected string altSoundStart = string.Empty;
    protected string altSoundLoop = string.Empty;
    protected string altSoundEnd = string.Empty;
    protected string altSoundEmpty = string.Empty;
    protected bool suppressFlashOnAlt = false;
    protected bool suppressFlashOnOrigin = false;
    private const string altModeAnimatorBool = "altMode";

    public bool isAltMode(EntityAlive holdingEntity)
    {
        return !string.IsNullOrEmpty(cvarStateSwitch) && holdingEntity && holdingEntity.GetCVar(cvarStateSwitch) > 0;
    }

    public virtual void setAltSound(bool isAlt, ItemActionData _actionData)
    {
        ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
        if (isAlt)
        {
            rangedData.SoundStart = altSoundStart;
            rangedData.SoundLoop = altSoundLoop;
            rangedData.SoundEnd = altSoundEnd;
            soundEmpty = altSoundEmpty;
            rangedData.IsFlashSuppressed = suppressFlashOnAlt;
        }
        else
        {
            rangedData.SoundStart = originalSoundStart;
            rangedData.SoundLoop = originalSoundLoop;
            rangedData.SoundEnd = originalSoundEnd;
            soundEmpty = originalSoundEmpty;
            rangedData.IsFlashSuppressed = suppressFlashOnOrigin;
        }
    }

    public virtual void setAltSound(ItemActionData _actionData)
    {
        setAltSound(isAltMode(_actionData.invData.holdingEntity), _actionData);
    }

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);

        _props.ParseString("Cvar_State_Switch", ref cvarStateSwitch);
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
        if (altSoundStart.Contains("silenced"))
            suppressFlashOnAlt = true;
        if (originalSoundStart.Contains("silenced"))
            suppressFlashOnOrigin = true;
    }

    public override void ItemActionEffects(GameManager _gameManager, ItemActionData _actionData, int _firingState, Vector3 _startPos, Vector3 _direction, int _userData = 0)
    {
        setAltSound(_actionData);
        base.ItemActionEffects(_gameManager, _actionData, _firingState, _startPos, _direction, _userData);
    }

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);

        if (GameManager.IsDedicatedServer || !(_actionData is ItemActionDataRanged))
            return;

        EntityAlive holdingEntity = _actionData.invData.holdingEntity;

        bool isCurAlt = isAltMode(holdingEntity);
        if (isCurAlt != getAnimatorBool(holdingEntity, altModeAnimatorBool))
        {
            setAnimatorBool(holdingEntity, altModeAnimatorBool, isCurAlt);
        }
    }
}

