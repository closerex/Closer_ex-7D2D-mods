using UnityEngine;

public class ItemActionRampUp : ItemActionRanged
{
    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        var _rampData = _actionData as ItemActionDataRampUp;
        if (!_bReleased && _actionData.invData.itemValue.Meta > 0 && _actionData.invData.itemValue.PercentUsesLeft > 0)
        {
            if (!_rampData.prepareStarted)
            {
                _rampData.prepareStarted = true;
                _rampData.prepareStartTime = Time.time;
                _rampData.invData.holdingEntity.PlayOneShot(_rampData.prepareSound);
            }
            if (Time.time - _rampData.prepareStartTime < _rampData.prepareTime)
                return;
        }
        base.ExecuteAction(_actionData, _bReleased);
        if (_rampData.state == ItemActionFiringState.Loop && _rampData.curBurstCount == _rampData.minRampShots)
        {
            _rampData.rampStarted = true;
            _rampData.rampStartTime = Time.time;
            _rampData.invData.holdingEntity.PlayOneShot(_rampData.rampSound);
        }
        else if (_rampData.bReleased)
        {
            _rampData.rampStarted = false;
            _rampData.prepareStarted = false;
        }
    }

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);
        var _rampData = _actionData as ItemActionDataRampUp;
        if(_rampData.rampStarted)
            _rampData.Delay *= Mathf.Min((Time.time - _rampData.rampStartTime) / _rampData.rampTime, 1) * _rampData.maxMultiplier;
    }

    public override void StopHolding(ItemActionData _data)
    {
        base.StopHolding(_data);
        var _rampData = _data as ItemActionDataRampUp;
        _rampData.rampStarted = false;
        _rampData.prepareStarted = false;
    }

    public override void ReloadGun(ItemActionData _actionData)
    {
        base.ReloadGun(_actionData);
        var _rampData = _actionData as ItemActionDataRampUp;
        _rampData.rampStarted = false;
        _rampData.prepareStarted = false;
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
        Properties.ParseString("RampStartSound", ref _rampData.rampSound);
        _rampData.rampSound = _rampData.invData.itemValue.GetPropertyOverride("RampStartSound", originalValue);

        originalValue = 0.ToString();
        Properties.ParseString("PrepareTime", ref originalValue);
        _rampData.prepareTime = float.Parse(_rampData.invData.itemValue.GetPropertyOverride("PrepareTime", originalValue));

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
        public string rampSound = string.Empty;
        public string prepareSound = string.Empty;
        public int minRampShots = 1;

        public float rampStartTime = 0f;
        public bool rampStarted = false;
        public float prepareStartTime = 0f;
        public bool prepareStarted = false;
    }
}

