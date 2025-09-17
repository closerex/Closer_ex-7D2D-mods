using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using UnityEngine;
using static ItemActionRanged;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(RampUpData))]
public class ActionModuleRampUp
{
    public enum State
    {
        RampUp,
        Stable,
        RampDown
    }

    private readonly static int prepareHash = Animator.StringToHash("prepare");
    private readonly static int prepareSpeedHash = Animator.StringToHash("prepareSpeed");
    private readonly static int rampHash = Animator.StringToHash("ramp");
    private readonly static int prepareRatioHash = Animator.StringToHash("prepareRatio");
    private readonly static int rampRatioHash = Animator.StringToHash("rampRatio");
    private readonly static int totalRatioHash = Animator.StringToHash("totalRatio");

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    public void Postfix_OnHoldingUpdate(ItemActionData _actionData, RampUpData __customData, ItemActionRanged __instance)
    {
        var rangedData = _actionData as ItemActionDataRanged;
        __customData.originalDelay = rangedData.Delay;
        if (rangedData.invData.holdingEntity.isEntityRemote)
            return;

        bool aiming = rangedData.invData.holdingEntity.AimingGun;
        bool isRampUp = ((rangedData.bPressed && !rangedData.bReleased && ItemActionRanged.NotReloading(rangedData) && rangedData.curBurstCount < __instance.GetBurstCount(rangedData)) || (__customData.zoomPrepare && aiming)) && (__instance.InfiniteAmmo || _actionData.invData.itemValue.Meta > 0) && _actionData.invData.itemValue.PercentUsesLeft > 0;
        UpdateTick(__customData, _actionData, isRampUp);
        if (__customData.rampRatio > 0)
        {
            rangedData.Delay /= __customData.rampRatio >= 1f ? __customData.maxMultiplier : __customData.rampRatio * (__customData.maxMultiplier - 1f) + 1f;
        }
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemActionData _data, RampUpData __customData, ItemActionRanged __instance)
    {
        int actionIndex = __instance.ActionIndex;
        string originalValue = 1.ToString();
        __instance.Properties.ParseString("RampMultiplier", ref originalValue);
        __customData.maxMultiplier = Mathf.Max(float.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("RampMultiplier", originalValue, actionIndex)), 1);

        originalValue = 0.ToString();
        __instance.Properties.ParseString("RampUpTime", ref originalValue);
        __customData.rampUpTime = float.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("RampTime", originalValue, actionIndex));

        originalValue = string.Empty;
        __instance.Properties.ParseString("RampUpSound", ref originalValue);
        __customData.rampUpSound = _data.invData.itemValue.GetPropertyOverrideForAction("RampStartSound", originalValue, actionIndex);

        originalValue = 0.ToString();
        __instance.Properties.ParseString("RampDownTime", ref originalValue);
        __customData.rampDownTime = Mathf.Max(float.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("RampTime", originalValue, actionIndex)), 0);

        originalValue = string.Empty;
        __instance.Properties.ParseString("RampDownSound", ref originalValue);
        __customData.rampDownSound = _data.invData.itemValue.GetPropertyOverrideForAction("RampStartSound", originalValue, actionIndex);

        originalValue = 0.ToString();
        __instance.Properties.ParseString("PrepareTime", ref originalValue);
        __customData.prepareTime = float.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("PrepareTime", originalValue, actionIndex));
        __customData.prepareSpeed = float.Parse(originalValue) / __customData.prepareTime;

        originalValue = string.Empty;
        __instance.Properties.ParseString("PrepareSound", ref originalValue);
        __customData.prepareSound = _data.invData.itemValue.GetPropertyOverrideForAction("PrepareSound", originalValue, actionIndex);

        originalValue = false.ToString();
        __instance.Properties.ParseString("PrepareOnAim", ref originalValue);
        __customData.zoomPrepare = bool.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("PrepareOnAim", originalValue, actionIndex));

        originalValue = string.Empty;
        __instance.Properties.ParseString("RampStableSound", ref originalValue);
        __customData.rampStableSound = _data.invData.itemValue.GetPropertyOverrideForAction("RampStableSound", originalValue, actionIndex);

        __customData.totalChargeTime = __customData.prepareTime + __customData.rampUpTime;
        __customData.rampDownTimeScale = __customData.rampDownTime > 0 ? (__customData.totalChargeTime) / __customData.rampDownTime : float.MaxValue;

        ResetAll(__customData, _data);
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(RampUpData __customData, ItemActionData _data)
    {
        ResetAll(__customData, _data);
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPrefix]
    public bool Prefix_ExecuteAction(RampUpData __customData, ItemActionRanged __instance, ItemActionData _actionData, bool _bReleased)
    {
        ItemActionDataRanged rangedData = _actionData as ItemActionDataRanged;
        if (!_bReleased && (__instance.InfiniteAmmo || _actionData.invData.itemValue.Meta > 0) && _actionData.invData.itemValue.PercentUsesLeft > 0)
        {
            rangedData.bReleased = false;
            rangedData.bPressed = true;
            if (__customData.curTime < __customData.prepareTime)
                return false;
        }
        return true;
    }

    private void UpdateTick(RampUpData data, ItemActionData actionData, bool isRampUp)
    {
        float previousTime = data.curTime;
        float deltaTime = Time.time - data.lastTickTime;
        data.lastTickTime = Time.time;
        ref float curTime = ref data.curTime;
        ref State curState = ref data.curState;
        float totalChargeTime = data.totalChargeTime;
        switch (curState)
        {
            case State.RampUp:
                {
                    curTime = Mathf.Max(curTime, 0);
                    if (isRampUp)
                    {
                        actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(prepareHash, true, true);
                        if (curTime < totalChargeTime)
                        {
                            curTime += deltaTime;
                        }
                        if (curTime >= data.prepareTime)
                        {
                            actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(rampHash, true, true);
                        }
                        if (curTime >= totalChargeTime)
                        {
                            //Log.Out($"change state from {curState} to stable");
                            actionData.invData.holdingEntity.PlayOneShot(data.rampStableSound);
                            curState = State.Stable;
                        }
                    }
                    else
                    {
                        //Log.Out($"change state from {curState} to ramp down");
                        actionData.invData.holdingEntity.StopOneShot(data.rampUpSound);
                        actionData.invData.holdingEntity.PlayOneShot(data.rampDownSound);
                        curState = State.RampDown;
                    }
                    break;
                }
            case State.RampDown:
                {
                    curTime = Mathf.Min(curTime, totalChargeTime);
                    if (!isRampUp)
                    {
                        actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(rampHash, false, true);
                        if (curTime > 0)
                        {
                            curTime -= deltaTime * data.rampDownTimeScale;
                        }
                        if (curTime < data.prepareTime)
                        {
                            actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(prepareHash, false, true);
                        }
                        if (curTime <= 0)
                        {
                            //Log.Out($"change state from {curState} to stable");
                            //actionData.invData.holdingEntity.PlayOneShot(data.rampStableSound);
                            curState = State.Stable;
                        }
                    }
                    else
                    {
                        //Log.Out($"change state from {curState} to ramp up");
                        actionData.invData.holdingEntity.StopOneShot(data.rampDownSound);
                        actionData.invData.holdingEntity.PlayOneShot(data.rampUpSound);
                        curState = State.RampUp;
                    }
                    break;
                }
            case State.Stable:
                {
                    if (isRampUp)
                    {
                        if (curTime < totalChargeTime)
                        {
                            //Log.Out($"change state from {curState} to ramp up");
                            actionData.invData.holdingEntity.StopOneShot(data.rampStableSound);
                            actionData.invData.holdingEntity.StopOneShot(data.rampDownSound);
                            actionData.invData.holdingEntity.PlayOneShot(data.rampUpSound);
                            curState = State.RampUp;
                        }
                        else
                        {
                            actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(prepareHash, true, true);
                            actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(rampHash, true, true);
                        }
                    }
                    else
                    {
                        if (curTime > 0)
                        {
                            //Log.Out($"change state from {curState} to ramp down");
                            actionData.invData.holdingEntity.StopOneShot(data.rampStableSound);
                            actionData.invData.holdingEntity.StopOneShot(data.rampUpSound);
                            actionData.invData.holdingEntity.PlayOneShot(data.rampDownSound);
                            curState = State.RampDown;
                        }
                        else
                        {
                            actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(prepareHash, false, true);
                            actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(rampHash, false, true);
                        }
                    }
                    break;
                }
        }
        //Log.Out($"turret burst fire rate {turret.burstFireRate} max {turret.burstFireRateMax} cur time {curTime} cur state {curState} is ramp up {isRampUp} turret: ison {turret.IsOn} has target {turret.hasTarget} state {turret.state}");
        actionData.invData.holdingEntity.emodel.avatarController.UpdateFloat(prepareSpeedHash, data.prepareSpeed);
        if (curTime != previousTime)
        {
            actionData.invData.holdingEntity.emodel.avatarController.UpdateFloat(prepareRatioHash, data.prepareRatio = (data.prepareTime == 0 ? 1f : Mathf.Clamp01(curTime / data.prepareTime)));
            actionData.invData.holdingEntity.emodel.avatarController.UpdateFloat(rampRatioHash, data.rampRatio = (data.rampUpTime == 0 ? 1f : Mathf.Clamp01((curTime - data.prepareTime) / data.rampUpTime)));
            actionData.invData.holdingEntity.emodel.avatarController.UpdateFloat(totalRatioHash, data.totalRatio = (totalChargeTime == 0 ? 1f : Mathf.Clamp01(curTime / totalChargeTime)));
        }
    }

    private void ResetAll(RampUpData _rampData, ItemActionData _actionData)
    {
        _rampData.curTime = 0f;
        _rampData.lastTickTime = Time.time;
        _rampData.curState = State.Stable;
        _rampData.prepareRatio = 0f;
        _rampData.rampRatio = 0f;
        _rampData.totalRatio = 0f;
        ((ItemActionDataRanged)_actionData).Delay = _rampData.originalDelay;
        _actionData.invData.holdingEntity.StopOneShot(_rampData.prepareSound);
        _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(prepareHash, false, true);
        _actionData.invData.holdingEntity.StopOneShot(_rampData.rampUpSound);
        _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(rampHash, false, true);
        //Log.Out("Reset all!");
    }

    public class RampUpData
    {
        public float maxMultiplier = 1f;

        public string prepareSound = string.Empty;
        public float prepareSpeed = 1f;
        public float prepareTime = 0f;

        public string rampUpSound = string.Empty;
        public float rampUpTime = 0f;
        public float totalChargeTime = 0f;

        public string rampDownSound = string.Empty;
        public float rampDownTime = 0f;
        public float rampDownTimeScale = float.MaxValue;

        public string rampStableSound = string.Empty;

        public float originalDelay = 0f;
        public float curTime = 0f;
        public State curState = State.Stable;
        public float prepareRatio = 0f;
        public float rampRatio = 0f;
        public float totalRatio = 0f;
        public float lastTickTime = 0f;

        public bool zoomPrepare = false;

        public ActionModuleRampUp rampUpModule;

        public RampUpData(ActionModuleRampUp __customModule)
        {
            rampUpModule = __customModule;
        }
    }
}