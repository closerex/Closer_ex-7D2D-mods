using Audio;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using UnityEngine;

public class ItemActionRampUp : ItemActionHoldOpen
{
    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        var _rampData = _actionData as ItemActionDataRampUp;
        if (!_bReleased && (InfiniteAmmo || _actionData.invData.itemValue.Meta > 0) && _actionData.invData.itemValue.PercentUsesLeft > 0)
        {
            _rampData.bReleased = false;
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
        var entity = _rampData.invData.holdingEntity;
        if (_firingState != 0)
        {
            if ((_userData & 2) > 0)
            {
                Manager.Stop(entity.entityId, _rampData.rampSound);
                _rampData.rampStarted = true;
                _rampData.rampStartTime = Time.time;
                Manager.Play(entity, _rampData.rampSound);
            }
        }
        else if ((_userData & 4) > 0)
        {
            //Log.Out("released, try aim charge!" + _userData);
            ResetRamp(_rampData);
            if (!_rampData.prepareStarted)
            {
                //Log.Out("released and aim charge!");
                Manager.Stop(entity.entityId, _rampData.prepareSound);
                _rampData.prepareStarted = true;
                _rampData.prepareStartTime = Time.time;
                Manager.Play(entity, _rampData.prepareSound);
                setAnimatorBool(_rampData.invData.holdingEntity, "prepare", true);
                setAnimatorFloat(_rampData.invData.holdingEntity, "prepareSpeed", _rampData.prepareSpeed);
            }
        }
        else
        {
            //Log.Out("released, reset all!" + _userData + entity.AimingGun);
            ResetAll(_rampData);
        }
    }

    public override int getUserData(ItemActionData _actionData)
    {
        var _rampData = _actionData as ItemActionDataRampUp;
        return base.getUserData(_actionData) | (Convert.ToInt32(_rampData.curBurstCount == _rampData.minRampShots) << 1) | (Convert.ToInt32(_rampData.zoomPrepare && _rampData.invData.holdingEntity.AimingGun) << 2);
    }

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);
        var _rampData = _actionData as ItemActionDataRampUp;
        if (_rampData.invData.holdingEntity.isEntityRemote)
            return;

        bool aiming = _rampData.invData.holdingEntity.AimingGun;
        if (!_rampData.prepareStarted && _rampData.zoomPrepare && aiming)
        {
            _rampData.invData.gameManager.ItemActionEffectsServer(_rampData.invData.holdingEntity.entityId, _rampData.invData.slotIdx, _rampData.indexInEntityOfAction, 0, Vector3.zero, Vector3.zero, 4);
            //Log.Out("Aim charge!");
        }
        else if (_rampData.prepareStarted && _rampData.bReleased && (!_rampData.zoomPrepare || !aiming))
        {
            _rampData.invData.gameManager.ItemActionEffectsServer(_rampData.invData.holdingEntity.entityId, _rampData.invData.slotIdx, _rampData.indexInEntityOfAction, 0, Vector3.zero, Vector3.zero, 0);
            //Log.Out("Stop charge!");
        }
        else if (_rampData.rampStarted)
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

    private void ResetAll(ItemActionDataRampUp _rampData)
    {
        ResetPrepare(_rampData);
        ResetRamp(_rampData);
        //Log.Out("Reset all!");
    }

    private void ResetPrepare(ItemActionDataRampUp _rampData)
    {
        _rampData.prepareStarted = false;
        Manager.Stop(_rampData.invData.holdingEntity.entityId, _rampData.prepareSound);
        setAnimatorBool(_rampData.invData.holdingEntity, "prepare", false);
        //Log.Out("Reset Prepare!");
    }

    private void ResetRamp(ItemActionDataRampUp _rampData)
    {
        _rampData.rampStarted = false;
        Manager.Stop(_rampData.invData.holdingEntity.entityId, _rampData.rampSound);
        //Log.Out("Reset Ramp!");
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
        _rampData.maxMultiplier = Mathf.Max(float.Parse(_rampData.invData.itemValue.GetPropertyOverrideForAction("RampMultiplier", originalValue, _data.indexInEntityOfAction)), 0);

        originalValue = 0.ToString();
        Properties.ParseString("RampTime", ref originalValue);
        _rampData.rampTime = float.Parse(_rampData.invData.itemValue.GetPropertyOverrideForAction("RampTime", originalValue, _data.indexInEntityOfAction));

        originalValue = 1.ToString();
        Properties.ParseString("MinRampShots", ref originalValue);
        _rampData.minRampShots = Mathf.Max(int.Parse(_rampData.invData.itemValue.GetPropertyOverrideForAction("MinRampShots", originalValue, _data.indexInEntityOfAction)), 1);

        originalValue = string.Empty;
        Properties.ParseString("RampStartSound", ref originalValue);
        _rampData.rampSound = _rampData.invData.itemValue.GetPropertyOverrideForAction("RampStartSound", originalValue, _data.indexInEntityOfAction);

        originalValue = 0.ToString();
        Properties.ParseString("PrepareTime", ref originalValue);
        _rampData.prepareTime = float.Parse(_rampData.invData.itemValue.GetPropertyOverrideForAction("PrepareTime", originalValue, _data.indexInEntityOfAction));
        _rampData.prepareSpeed = float.Parse(originalValue) / _rampData.prepareTime;

        originalValue = string.Empty;
        Properties.ParseString("PrepareSound", ref originalValue);
        _rampData.prepareSound = _rampData.invData.itemValue.GetPropertyOverrideForAction("PrepareSound", originalValue, _data.indexInEntityOfAction);

        originalValue = false.ToString();
        Properties.ParseString("PrepareOnAim", ref originalValue);
        _rampData.zoomPrepare = bool.Parse(_rampData.invData.itemValue.GetPropertyOverrideForAction("PrepareOnAim", originalValue, _data.indexInEntityOfAction));
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
        public bool zoomPrepare = false;
    }
}

