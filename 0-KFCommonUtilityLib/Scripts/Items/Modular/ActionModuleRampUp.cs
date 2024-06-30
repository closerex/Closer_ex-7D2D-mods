using Audio;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using UnityEngine;
using static ItemActionRanged;

[TypeTarget(typeof(ItemActionRanged), typeof(RampUpData))]
public class ActionModuleRampUp
{
    [MethodTargetPostfix(nameof(ItemActionRanged.ItemActionEffects))]
    public void Postfix_ItemActionEffects(RampUpData __customData, ItemActionData _actionData, int _firingState, int _userData)
    {
        var entity = _actionData.invData.holdingEntity;
        if (_firingState != 0)
        {
            if ((_userData & 2) > 0)
            {
                Manager.Stop(entity.entityId, __customData.rampSound);
                __customData.rampStarted = true;
                __customData.rampStartTime = Time.time;
                Manager.Play(entity, __customData.rampSound);
            }
        }
        else if ((_userData & 4) > 0)
        {
            //Log.Out("released, try aim charge!" + _userData);
            ResetRamp(__customData, _actionData);
            if (!__customData.prepareStarted)
            {
                //Log.Out("released and aim charge!");
                Manager.Stop(entity.entityId, __customData.prepareSound);
                __customData.prepareStarted = true;
                __customData.prepareStartTime = Time.time;
                Manager.Play(entity, __customData.prepareSound);

                entity.emodel.avatarController.UpdateBool("prepare", true, true);
                entity.emodel.avatarController.UpdateFloat("prepareSpeed", __customData.prepareSpeed, true);
            }
        }
        else
        {
            //Log.Out("released, reset all!" + _userData + entity.AimingGun);
            ResetAll(__customData, _actionData);
        }
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.OnHoldingUpdate))]
    public void Postfix_OnHoldingUpdate(ItemActionData _actionData, RampUpData __customData)
    {
        var rangedData = _actionData as ItemActionDataRanged;
        if (rangedData.invData.holdingEntity.isEntityRemote)
            return;

        bool aiming = rangedData.invData.holdingEntity.AimingGun;
        if (!__customData.prepareStarted && __customData.zoomPrepare && aiming)
        {
            rangedData.invData.gameManager.ItemActionEffectsServer(rangedData.invData.holdingEntity.entityId, rangedData.invData.slotIdx, rangedData.indexInEntityOfAction, 0, Vector3.zero, Vector3.zero, 4);
            //Log.Out("Aim charge!");
        }
        else if (__customData.prepareStarted && rangedData.bReleased && (!__customData.zoomPrepare || !aiming))
        {
            rangedData.invData.gameManager.ItemActionEffectsServer(rangedData.invData.holdingEntity.entityId, rangedData.invData.slotIdx, rangedData.indexInEntityOfAction, 0, Vector3.zero, Vector3.zero, 0);
            //Log.Out("Stop charge!");
        }
        else if (__customData.rampStarted)
        {
            float rampElapsed = Time.time - __customData.rampStartTime;
            if (rampElapsed > 0)
                rangedData.Delay /= rampElapsed > __customData.rampTime ? __customData.maxMultiplier : rampElapsed * (__customData.maxMultiplier - 1) / __customData.rampTime + 1;
        }
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.OnModificationsChanged))]
    public void Postfix_OnModificationsChanged(ItemActionData _data, RampUpData __customData, ItemActionRanged __instance)
    {
        int actionIndex = __instance.ActionIndex;
        string originalValue = 1.ToString();
        __instance.Properties.ParseString("RampMultiplier", ref originalValue);
        __customData.maxMultiplier = Mathf.Max(float.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("RampMultiplier", originalValue, actionIndex)), 0);

        originalValue = 0.ToString();
        __instance.Properties.ParseString("RampTime", ref originalValue);
        __customData.rampTime = float.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("RampTime", originalValue, actionIndex));

        originalValue = 1.ToString();
        __instance.Properties.ParseString("MinRampShots", ref originalValue);
        __customData.minRampShots = Mathf.Max(int.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("MinRampShots", originalValue, actionIndex)), 1);

        originalValue = string.Empty;
        __instance.Properties.ParseString("RampStartSound", ref originalValue);
        __customData.rampSound = _data.invData.itemValue.GetPropertyOverrideForAction("RampStartSound", originalValue, actionIndex);

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
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StopHolding))]
    public void Postfix_StopHolding(RampUpData __customData, ItemActionData _data)
    {
        ResetRamp(__customData, _data);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.ExecuteAction))]
    public bool Prefix_ExecuteAction(RampUpData __customData, ItemActionRanged __instance, ItemActionData _actionData, bool _bReleased)
    {
        ItemActionDataRanged rangedData = _actionData as ItemActionDataRanged;
        if (!_bReleased && (__instance.InfiniteAmmo || _actionData.invData.itemValue.Meta > 0) && _actionData.invData.itemValue.PercentUsesLeft > 0)
        {
            rangedData.bReleased = false;
            if (!__customData.prepareStarted)
                rangedData.invData.gameManager.ItemActionEffectsServer(rangedData.invData.holdingEntity.entityId, rangedData.invData.slotIdx, rangedData.indexInEntityOfAction, 0, Vector3.zero, Vector3.zero, 4);

            if (Time.time - __customData.prepareStartTime < __customData.prepareTime)
                return false;
        }
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.ReloadGun))]
    public void ReloadGun(RampUpData __customData, ItemActionData _actionData)
    {
        ResetRamp(__customData, _actionData);
    }

    [MethodTargetPostfix("getUserData")]
    protected void Postfix_getUserData(ItemActionData _actionData, ref int __result, RampUpData __customData)
    {
        ItemActionDataRanged rangedData = _actionData as ItemActionDataRanged;
        __result |= (Convert.ToInt32(rangedData.curBurstCount == __customData.minRampShots) << 1) | (Convert.ToInt32(__customData.zoomPrepare && rangedData.invData.holdingEntity.AimingGun) << 2);
    }
    private void ResetAll(RampUpData _rampData, ItemActionData _actionData)
    {
        ResetPrepare(_rampData, _actionData);
        ResetRamp(_rampData, _actionData);
        //Log.Out("Reset all!");
    }

    private void ResetPrepare(RampUpData _rampData, ItemActionData _actionData)
    {
        _rampData.prepareStarted = false;
        Manager.Stop(_actionData.invData.holdingEntity.entityId, _rampData.prepareSound);
        _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool("prepare", false, true);
        //Log.Out("Reset Prepare!");
    }

    private void ResetRamp(RampUpData _rampData, ItemActionData _actionData)
    {
        _rampData.rampStarted = false;
        Manager.Stop(_actionData.invData.holdingEntity.entityId, _rampData.rampSound);
        //Log.Out("Reset Ramp!");
    }
    public class RampUpData
    {
        public float maxMultiplier = 1f;

        public int minRampShots = 1;

        public string prepareSound = string.Empty;

        public float prepareSpeed = 1f;

        public bool prepareStarted = false;

        public float prepareStartTime = 0f;

        public float prepareTime = 0f;

        public string rampSound = string.Empty;

        public bool rampStarted = false;

        public float rampStartTime = 0f;

        public float rampTime = 0f;

        public bool zoomPrepare = false;

        public ActionModuleRampUp rampUpModule;

        public RampUpData(ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleRampUp _module)
        {
            rampUpModule = _module;
        }
    }
}