using UnityEngine;

class ItemActionAltMode : ItemActionHoldOpen
{
    protected string cvarStateSwitch = null;
    protected string originalSoundStart = string.Empty;
    protected string originalSoundLoop = string.Empty;
    protected string originalSoundEnd = string.Empty;
    protected string originalSoundEmpty = string.Empty;
    protected string[] altSoundStart = null;
    protected string[] altSoundLoop = null;
    protected string[] altSoundEnd = null;
    protected string[] altSoundEmpty = null;
    protected bool[] altInfiniteAmmo = null;
    protected bool originalInfiniteAmmo = false;
    protected bool[] suppressFlashOnAlt = null;
    protected bool suppressFlashOnOrigin = false;
    private string altModeAnimatorBool = "altMode";

    public int getCurAltIndex(EntityAlive holdingEntity)
    {
        return MathUtils.Max((int)holdingEntity.GetCVar(cvarStateSwitch), 0) - 1;
    }

    public virtual void setAltSound(ItemActionData _actionData)
    {
        ItemActionDataAltMode _data = _actionData as ItemActionDataAltMode;
        int altIndex = _data.modeIndex;
        if (altIndex >= 0)
        {
            _data.SoundStart = altSoundStart.Length > altIndex ? altSoundStart[altIndex] : string.Empty;
            _data.SoundLoop = altSoundLoop.Length > altIndex ? altSoundLoop[altIndex] : string.Empty;
            _data.SoundEnd = altSoundEnd.Length > altIndex ? altSoundEnd[altIndex] : string.Empty;
            soundEmpty = altSoundEmpty.Length > altIndex ? altSoundEmpty[altIndex] : string.Empty;
            _data.IsFlashSuppressed = suppressFlashOnAlt.Length > altIndex ? suppressFlashOnAlt[altIndex] : false;
        }
        else
        {
            _data.SoundStart = originalSoundStart;
            _data.SoundLoop = originalSoundLoop;
            _data.SoundEnd = originalSoundEnd;
            soundEmpty = originalSoundEmpty;
            _data.IsFlashSuppressed = suppressFlashOnOrigin;
        }
    }

    public override ItemActionData CreateModifierData(ItemInventoryData _invData, int _indexInEntityOfAction)
    {
        return new ItemActionDataAltMode(_invData, _indexInEntityOfAction, cvarStateSwitch);
    }

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);

        string _altString = string.Empty;
        /*
        _props.ParseInt("Mode_Count", ref modeCount);
        altAmmoName = new string[modeCount][];
        for(int i = 0; i < modeCount; ++i)
        {
            _altString = string.Empty;
            _props.ParseString("Alt_Magazine_Items", ref _altString);
            altAmmoName[i] = _altString.Split(',');
        }
        */
        _props.ParseString("Cvar_State_Switch", ref cvarStateSwitch);
        _props.ParseString("Sound_start", ref originalSoundStart);
        _props.ParseString("Sound_loop", ref originalSoundLoop);
        _props.ParseString("Sound_end", ref originalSoundEnd);
        _props.ParseString("Sound_empty", ref originalSoundEmpty);
        _altString = string.Empty;
        _props.ParseString("Alt_Sound_Start", ref _altString);
        altSoundStart = _altString.Split(',');
        suppressFlashOnAlt = new bool[altSoundStart.Length];
        for(int i = 0; i < suppressFlashOnAlt.Length; ++i)
        {
            if (altSoundStart[i].Contains("silenced"))
                suppressFlashOnAlt[i] = true;
        }
        _altString = string.Empty;
        _props.ParseString("Alt_Sound_Loop", ref _altString);
        altSoundLoop = _altString.Split(',');
        _altString = string.Empty;
        _props.ParseString("Alt_Sound_End", ref _altString);
        altSoundEnd = _altString.Split(',');
        _altString = string.Empty;
        _props.ParseString("Alt_Sound_Empty", ref _altString);
        altSoundEmpty = _altString.Split(',');
        _altString = string.Empty;
        _props.ParseString("Alt_InfiniteAmmo", ref _altString);
        string[] _altInfiniteAmmo = _altString.Split(',');
        altInfiniteAmmo = new bool[_altInfiniteAmmo.Length];
        for (int i = 0; i < altInfiniteAmmo.Length; ++i)
            altInfiniteAmmo[i] = bool.Parse(_altInfiniteAmmo[i]);
        originalInfiniteAmmo = InfiniteAmmo;
        if (originalSoundStart.Contains("silenced"))
            suppressFlashOnOrigin = true;
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        ItemActionDataAltMode _data = _actionData as ItemActionDataAltMode;
        int curAltIndex = _data.modeIndex;

        if (!_bReleased && curAltIndex >= 0)
            InfiniteAmmo = altInfiniteAmmo.Length > curAltIndex ? altInfiniteAmmo[curAltIndex] : false;
        else
            InfiniteAmmo = originalInfiniteAmmo;
        base.ExecuteAction(_actionData, _bReleased);
    }

    public override void ItemActionEffects(GameManager _gameManager, ItemActionData _actionData, int _firingState, Vector3 _startPos, Vector3 _direction, int _userData = 0)
    {
        if(_firingState != 0)
            setAltSound(_actionData);
        base.ItemActionEffects(_gameManager, _actionData, _firingState, _startPos, _direction, _userData);
    }

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);

        if (GameManager.IsDedicatedServer || !(_actionData is ItemActionDataAltMode))
            return;

        ItemActionDataAltMode _data = _actionData as ItemActionDataAltMode;
        EntityAlive holdingEntity = _data.invData.holdingEntity;

        int altIndex = getCurAltIndex(holdingEntity);
        if(altIndex != _data.modeIndex)
        {
            if (_data.modeIndex >= 0)
                setAnimatorBool(holdingEntity, altModeAnimatorBool + (_data.modeIndex + 1).ToString(), false);
            if(altIndex >= 0)
                setAnimatorBool(holdingEntity, altModeAnimatorBool + (altIndex + 1).ToString(), true);
            _data.modeIndex = altIndex;
        }
    }

    public class ItemActionDataAltMode : ItemActionDataRanged
    {
        public ItemActionDataAltMode(ItemInventoryData _invData, int _indexInEntityOfAction, string cvar_switch) : base(_invData, _indexInEntityOfAction)
        {
        }

        public int modeIndex = -1;
    }
}

