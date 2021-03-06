using UnityEngine;
using Audio;
using System;

public class ItemActionRampUp : ItemActionHoldOpen
{
    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        var _rampData = _actionData as ItemActionDataRampUp;
        if (!_bReleased && (InfiniteAmmo || _actionData.invData.itemValue.Meta > 0) && _actionData.invData.itemValue.PercentUsesLeft > 0)
        {
            if (!_rampData.prepareStarted)
                _rampData.invData.gameManager.ItemActionEffectsServer(_rampData.invData.holdingEntity.entityId, _rampData.invData.slotIdx, _rampData.indexInEntityOfAction, 0, Vector3.zero, Vector3.zero, 4);
            
            if (Time.time - _rampData.prepareStartTime < _rampData.prepareTime)
                return;
        }
        base.ExecuteAction(_actionData, _bReleased);
    }

    public override void ItemActionEffects(GameManager _gameManager, ItemActionData _actionData, int _firingState, Vector3 _startPos, Vector3 _direction, int _userData = 0)
    {
        base.ItemActionEffects(_gameManager, _actionData, _firingState, _startPos, _direction, _userData);
        var _rampData = _actionData as ItemActionDataRampUp;
        if (_firingState != 0 && (_userData & 2) > 0)
        {
            Manager.Stop(_rampData.invData.holdingEntity.entityId, _rampData.rampSound);
            _rampData.rampStarted = true;
            _rampData.rampStartTime = Time.time;
            Manager.Play(_rampData.invData.holdingEntity, _rampData.rampSound);
        }
        else if (_firingState == 0)
        {
            if((_userData & 4) > 0)
            {
                _rampData.invData.holdingEntity.StopOneShot(_rampData.prepareSound);
                _rampData.prepareStarted = true;
                _rampData.prepareStartTime = Time.time;
                _rampData.invData.holdingEntity.PlayOneShot(_rampData.prepareSound);
                setAnimatorBool(_rampData.invData.holdingEntity, "prepare", true);
                setAnimatorFloat(_rampData.invData.holdingEntity, "prepareSpeed", _rampData.prepareSpeed);
            }
            else
                ResetRamp(_rampData);
        }
    }

    protected override int getUserData(ItemActionData _actionData)
    {
        var _rampData = _actionData as ItemActionDataRampUp;
        return base.getUserData(_actionData) | (Convert.ToInt32(_rampData.curBurstCount == _rampData.minRampShots) << 1);
    }

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);
        var _rampData = _actionData as ItemActionDataRampUp;
        if(_rampData.rampStarted)
        {
            float rampElapsed = Time.time - _rampData.rampStartTime;
            if (rampElapsed > 0)
                _rampData.Delay /= rampElapsed > _rampData.rampTime ? _rampData.maxMultiplier : rampElapsed * (_rampData.maxMultiplier - 1) / _rampData.rampTime + 1;
        }
    }

    public override void StopHolding(ItemActionData _data)
    {
        base.StopHolding(_data);
        var _rampData = _data as ItemActionDataRampUp;
        ResetRamp(_rampData);
    }

    public override void ReloadGun(ItemActionData _actionData)
    {
        base.ReloadGun(_actionData);
        var _rampData = _actionData as ItemActionDataRampUp;
        ResetRamp(_rampData);
    }

    private void ResetRamp(ItemActionDataRampUp _rampData)
    {
        _rampData.rampStarted = false;
        _rampData.prepareStarted = false;
        _rampData.invData.holdingEntity.StopOneShot(_rampData.prepareSound);
        _rampData.invData.holdingEntity.StopOneShot(_rampData.rampSound);
        setAnimatorBool(_rampData.invData.holdingEntity, "prepare", false);
    }

    public override ItemActionData CreateModifierData(ItemInventoryData _invData, int _indexInEntityOfAction)
    {
        return new ItemActionDataRampUp(_invData, _indexInEntityOfAction);
    }

    public override void OnModificationsChanged(ItemActionData _data)
    {
        base.OnModificationsChanged(_data);
        var _rampData = _data as ItemActionDataRampUp;
        string originalValue = 1.ToString();
        Properties.ParseString("RampMultiplier", ref originalValue);
        _rampData.maxMultiplier = Mathf.Max(float.Parse(_rampData.invData.itemValue.GetPropertyOverride("RampMultiplier", originalValue)), 0);

        originalValue = 0.ToString();
        Properties.ParseString("RampTime", ref originalValue);
        _rampData.rampTime = float.Parse(_rampData.invData.itemValue.GetPropertyOverride("RampTime", originalValue));

        originalValue = 1.ToString();
        Properties.ParseString("MinRampShots", ref originalValue);
        _rampData.minRampShots = Mathf.Max(int.Parse(_rampData.invData.itemValue.GetPropertyOverride("MinRampShots", originalValue)), 1);

        originalValue = string.Empty;
        Properties.ParseString("RampStartSound", ref originalValue);
        _rampData.rampSound = _rampData.invData.itemValue.GetPropertyOverride("RampStartSound", originalValue);

        originalValue = 0.ToString();
        Properties.ParseString("PrepareTime", ref originalValue);
        _rampData.prepareTime = float.Parse(_rampData.invData.itemValue.GetPropertyOverride("PrepareTime", originalValue));
        _rampData.prepareSpeed = float.Parse(originalValue) / _rampData.prepareTime;

        originalValue = string.Empty;
        Properties.ParseString("PrepareSound", ref originalValue);
        _rampData.prepareSound = _rampData.invData.itemValue.GetPropertyOverride("PrepareSound", originalValue);
    }

    public class ItemActionDataRampUp : ItemActionDataRanged
    {
        public ItemActionDataRampUp(ItemInventoryData _invData, int _indexInEntityOfAction) : base(_invData, _indexInEntityOfAction)
        {
        }

        public float maxMultiplier = 1f;
        public float rampTime = 0f;
        public float prepareTime = 0f;
        public float prepareSpeed = 1f;
        public string rampSound = string.Empty;
        public string prepareSound = string.Empty;
        public int minRampShots = 1;

        public float rampStartTime = 0f;
        public bool rampStarted = false;
        public float prepareStartTime = 0f;
        public bool prepareStarted = false;
    }
}

