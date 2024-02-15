using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class ItemActionAltMode : ItemActionHoldOpen
{
    protected string cvarStateSwitch = null;
    protected bool[] altInfiniteAmmo = null;
    protected bool originalInfiniteAmmo = false;
    private string altModeAnimatorBool = "altMode";
    protected List<IRequirement>[] altRequirements;

    public int getCurAltIndex(EntityAlive holdingEntity)
    {
        return MathUtils.Max((int)holdingEntity.GetCVar(cvarStateSwitch), 0) - 1;
    }

    public virtual void setAltSound(ItemActionData _actionData)
    {
        ItemActionDataAltMode _data = _actionData as ItemActionDataAltMode;
        _data.SetAltSound();
        int altIndex = _data.modeIndex;
        if (altIndex >= 0)
            soundEmpty = _data.altSoundEmpty.Length > altIndex ? _data.altSoundEmpty[altIndex] : string.Empty;
        else
            soundEmpty = _data.originalSoundEmpty;
    }

    public override void OnModificationsChanged(ItemActionData _data)
    {
        base.OnModificationsChanged(_data);
        var _dataAlt = _data as ItemActionDataAltMode;

        string originalValue = "";
        Properties.ParseString("Sound_start", ref originalValue);
        _dataAlt.originalSoundStart = _dataAlt.invData.itemValue.GetPropertyOverride("Sound_start", originalValue);
        if (_dataAlt.originalSoundStart.Contains("silenced"))
            _dataAlt.suppressFlashOnOrigin = true;

        originalValue = "";
        Properties.ParseString("Sound_loop", ref originalValue);
        _dataAlt.originalSoundLoop = _dataAlt.invData.itemValue.GetPropertyOverride("Sound_loop", originalValue);

        originalValue = "";
        Properties.ParseString("Sound_end", ref originalValue);
        _dataAlt.originalSoundEnd = _dataAlt.invData.itemValue.GetPropertyOverride("Sound_end", originalValue);

        originalValue = "";
        Properties.ParseString("Sound_empty", ref originalValue);
        _dataAlt.originalSoundEmpty = _dataAlt.invData.itemValue.GetPropertyOverride("Sound_empty", originalValue);


        string _altString = string.Empty;
        Properties.ParseString("Alt_Sound_Start", ref _altString);
        _altString = _dataAlt.invData.itemValue.GetPropertyOverride("Alt_Sound_Start", _altString);
        _dataAlt.altSoundStart = _altString.Split(',');
        _dataAlt.suppressFlashOnAlt = new bool[_dataAlt.altSoundStart.Length];
        for (int i = 0; i < _dataAlt.suppressFlashOnAlt.Length; ++i)
        {
            if (_dataAlt.altSoundStart[i].Contains("silenced"))
                _dataAlt.suppressFlashOnAlt[i] = true;
        }

        _altString = string.Empty;
        Properties.ParseString("Alt_Sound_Loop", ref _altString);
        _altString = _dataAlt.invData.itemValue.GetPropertyOverride("Alt_Sound_Loop", _altString);
        _dataAlt.altSoundLoop = _altString.Split(',');

        _altString = string.Empty;
        Properties.ParseString("Alt_Sound_End", ref _altString);
        _altString = _dataAlt.invData.itemValue.GetPropertyOverride("Alt_Sound_End", _altString);
        _dataAlt.altSoundEnd = _altString.Split(',');

        _altString = string.Empty;
        Properties.ParseString("Alt_Sound_Empty", ref _altString);
        _altString = _dataAlt.invData.itemValue.GetPropertyOverride("Alt_Sound_Empty", _altString);
        _dataAlt.altSoundEmpty = _altString.Split(',');
    }

    public override ItemActionData CreateModifierData(ItemInventoryData _invData, int _indexInEntityOfAction)
    {
        return new ItemActionDataAltMode(_invData, _indexInEntityOfAction, cvarStateSwitch);
    }

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);

        string _altString = string.Empty;
        _props.ParseString("Cvar_State_Switch", ref cvarStateSwitch);
        _props.ParseString("Alt_InfiniteAmmo", ref _altString);
        string[] _altInfiniteAmmo = _altString.Split(',');
        altInfiniteAmmo = new bool[_altInfiniteAmmo.Length];
        for (int i = 0; i < altInfiniteAmmo.Length; ++i)
            altInfiniteAmmo[i] = bool.Parse(_altInfiniteAmmo[i]);
        originalInfiniteAmmo = InfiniteAmmo;

        altRequirements = new List<IRequirement>[_altInfiniteAmmo.Length + 1];
    }

    public void ParseAltRequirements(XElement _node, int _actionIdx)
    {
        foreach (XElement elem in _node.Elements("property"))
        {
            if (elem.HasAttribute("class") && elem.GetAttribute("class").Contains(_actionIdx.ToString()))
            {
                for (int i = 0; i < altRequirements.Length; ++i)
                {
                    var requirements = new List<IRequirement>();
                    requirements.AddRange(ExecutionRequirements);
                    foreach (XElement childElem in elem.Elements())
                    {
                        if (childElem.Name.LocalName.Equals("requirements" + i))
                        {
                            requirements.AddRange(RequirementBase.ParseRequirements(childElem));
                            break;
                        }
                    }
                    altRequirements[i] = requirements;
                }
                break;
            }
        }
    }

    public void SetAltRequirement(ItemActionData _actionData)
    {
        if (_actionData is ItemActionDataAltMode _data)
            ExecutionRequirements = altRequirements[_data.modeIndex + 1];
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
        if (_firingState != 0)
            setAltSound(_actionData);
        base.ItemActionEffects(_gameManager, _actionData, _firingState, _startPos, _direction, _userData);
    }

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);

        if (GameManager.IsDedicatedServer || !(_actionData is ItemActionDataAltMode _data))
            return;

        EntityAlive holdingEntity = _data.invData.holdingEntity;

        int altIndex = getCurAltIndex(holdingEntity);
        if (altIndex != _data.modeIndex)
        {
            if (_data.modeIndex >= 0)
                setAnimatorBool(holdingEntity, altModeAnimatorBool + (_data.modeIndex + 1).ToString(), false);
            if (altIndex >= 0)
                setAnimatorBool(holdingEntity, altModeAnimatorBool + (altIndex + 1).ToString(), true);
            _data.modeIndex = altIndex;
        }
    }

    public class ItemActionDataAltMode : ItemActionDataRanged
    {
        public ItemActionDataAltMode(ItemInventoryData _invData, int _indexInEntityOfAction, string cvar_switch) : base(_invData, _indexInEntityOfAction)
        {
        }

        public void SetAltSound()
        {
            if (modeIndex >= 0)
            {
                SoundStart = altSoundStart.Length > modeIndex ? altSoundStart[modeIndex] : string.Empty;
                SoundLoop = altSoundLoop.Length > modeIndex ? altSoundLoop[modeIndex] : string.Empty;
                SoundEnd = altSoundEnd.Length > modeIndex ? altSoundEnd[modeIndex] : string.Empty;
                IsFlashSuppressed = suppressFlashOnAlt.Length > modeIndex ? suppressFlashOnAlt[modeIndex] : false;
            }
            else
            {
                SoundStart = originalSoundStart;
                SoundLoop = originalSoundLoop;
                SoundEnd = originalSoundEnd;
                IsFlashSuppressed = suppressFlashOnOrigin;
            }
        }

        public int modeIndex = -1;
        public string originalSoundStart = string.Empty;
        public string originalSoundLoop = string.Empty;
        public string originalSoundEnd = string.Empty;
        public string originalSoundEmpty = string.Empty;
        public string[] altSoundStart = null;
        public string[] altSoundLoop = null;
        public string[] altSoundEnd = null;
        public string[] altSoundEmpty = null;
        public bool suppressFlashOnOrigin = false;
        public bool[] suppressFlashOnAlt;
    }
}

