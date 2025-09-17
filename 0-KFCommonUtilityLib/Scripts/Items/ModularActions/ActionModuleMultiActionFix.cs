using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;

[TypeTarget(typeof(ItemActionAttack)), TypeDataTarget(typeof(MultiActionData))]
public class ActionModuleMultiActionFix
{
    private int actionIndex;
    public string GetDisplayType(ItemValue itemValue)
    {
        string displayType = itemValue.GetPropertyOverrideForAction("DisplayType", null, actionIndex);
        if (string.IsNullOrEmpty(displayType))
        {
            displayType = itemValue.ItemClass.DisplayType;
        }
        return displayType;
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(ItemActionAttack __instance)
    {
        actionIndex = __instance.ActionIndex;
    }

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPrefix]
    public bool Prefix_StartHolding(ItemActionData _data, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_data, out __state);
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(ItemActionData _data, ItemActionData __state)
    {
        RestoreItemActionData(_data, __state);
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationChanged_ItemActionRanged(ItemActionData _data, ItemActionAttack __instance)
    {
        var rangedData = _data as ItemActionRanged.ItemActionDataRanged;
        if (rangedData != null)
        {
            string muzzleName;
            string indexExtension = (_data.indexInEntityOfAction > 0 ? _data.indexInEntityOfAction.ToString() : "");
            if (rangedData.IsDoubleBarrel)
            {
                muzzleName = _data.invData.itemValue.GetPropertyOverrideForAction($"Muzzle_L_Name", $"Muzzle_L{indexExtension}", _data.indexInEntityOfAction);
                rangedData.muzzle = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, muzzleName) ?? rangedData.muzzle;
                muzzleName = _data.invData.itemValue.GetPropertyOverrideForAction($"Muzzle_R_Name", $"Muzzle_R{indexExtension}", _data.indexInEntityOfAction);
                rangedData.muzzle2 = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, muzzleName) ?? rangedData.muzzle2;
            }
            else
            {
                muzzleName = _data.invData.itemValue.GetPropertyOverrideForAction($"Muzzle_Name", $"Muzzle{indexExtension}", _data.indexInEntityOfAction);
                rangedData.muzzle = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, muzzleName) ?? rangedData.muzzle;
            }
        }
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationChanged_ItemActionLauncher(ItemActionData _data, ItemActionAttack __instance)
    {
        Postfix_OnModificationChanged_ItemActionRanged(_data, __instance);
        if (_data is ItemActionLauncher.ItemActionDataLauncher launcherData)
        {
            string indexExtension = (_data.indexInEntityOfAction > 0 ? _data.indexInEntityOfAction.ToString() : "");
            string jointName = _data.invData.itemValue.GetPropertyOverrideForAction($"ProjectileJoint_Name", $"ProjectileJoint{indexExtension}", _data.indexInEntityOfAction);
            launcherData.projectileJointT = AnimationRiggingManager.GetTransformOverrideByName(launcherData.invData.model, jointName) ?? launcherData.projectileJointT;
        }
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPrefix]
    public bool Prefix_StopHolding(ItemActionData _data, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_data, out __state);
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(ItemActionData _data, ItemActionData __state)
    {
        RestoreItemActionData(_data, __state);
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemAction.ItemActionEffects)), MethodTargetPrefix]
    public bool Prefix_ItemActionEffects(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemAction.ItemActionEffects)), MethodTargetPostfix]
    public void Postfix_ItemActionEffects(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [HarmonyPatch(nameof(ItemAction.CancelAction)), MethodTargetPrefix]
    public bool Prefix_CancelAction(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.CancelAction)), MethodTargetPostfix]
    public void Postfix_CancelAction(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [HarmonyPatch(nameof(ItemActionAttack.CancelReload)), MethodTargetPrefix]
    public bool Prefix_CancelReload(ItemActionData _actionData, out ItemActionData __state)
    {
        SetAndSaveItemActionData(_actionData, out __state);
        return true;
    }

    [HarmonyPatch(nameof(ItemActionAttack.CancelReload)), MethodTargetPostfix]
    public void Postfix_CancelReload(ItemActionData _actionData, ItemActionData __state)
    {
        RestoreItemActionData(_actionData, __state);
    }

    [HarmonyPatch(nameof(ItemActionAttack.ReloadGun)), MethodTargetPrefix]
    public bool Prefix_ReloadGun(ItemActionData _actionData)
    {
        //int reloadAnimationIndex = MultiActionManager.GetMetaIndexForActionIndex(_actionData.invData.holdingEntity.entityId, _actionData.indexInEntityOfAction);
        _actionData.invData.holdingEntity.emodel?.avatarController?.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, _actionData.indexInEntityOfAction, false);
        _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
        //MultiActionManager.GetMappingForEntity(_actionData.invData.holdingEntity.entityId)?.SaveMeta();
        return true;
    }

    [HarmonyPatch(nameof(ItemAction.OnHUD)), MethodTargetPrefix]
    public bool Prefix_OnHUD(ItemActionData _actionData)
    {
        if (_actionData.invData?.holdingEntity?.MinEventContext?.ItemActionData == null || _actionData.indexInEntityOfAction != _actionData.invData.holdingEntity.MinEventContext.ItemActionData.indexInEntityOfAction)
            return false;
        return true;
    }

    //[MethodTargetPrefix(nameof(ItemActionAttack.ExecuteAction), typeof(ItemActionRanged))]
    //public bool Prefix_ExecuteAction(ItemActionData _actionData, MultiActionData __customData)
    //{
    //    //when executing action, set last action index so that correct accuracy is used for drawing crosshair
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
    //    {
    //        ((ItemActionRanged.ItemActionDataRanged)_actionData).lastAccuracy = __customData.lastAccuracy;
    //    }
    //    return true;
    //}

    //[MethodTargetPrefix("updateAccuracy", typeof(ItemActionRanged))]
    //public bool Prefix_updateAccuracy(ItemActionData _actionData, MultiActionData __customData)
    //{
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntityID(player.entityId) == _actionData.indexInEntityOfAction)
    //        return true;
    //    //always update custom accuracy
    //    ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
    //    (rangedData.lastAccuracy, __customData.lastAccuracy) = (__customData.lastAccuracy, rangedData.lastAccuracy);
    //    return true;
    //}

    //[MethodTargetPostfix("updateAccuracy", typeof(ItemActionRanged))]
    //public void Postfix_updateAccuracy(ItemActionData _actionData, MultiActionData __customData)
    //{
    //    //retain rangedData accuracy if it's the last executed action
    //    ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntityID(player.entityId) == _actionData.indexInEntityOfAction)
    //    {
    //        __customData.lastAccuracy = rangedData.lastAccuracy;
    //    }
    //    else
    //    {
    //        (rangedData.lastAccuracy, __customData.lastAccuracy) = (__customData.lastAccuracy, rangedData.lastAccuracy);
    //    }
    //}
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.onHoldingEntityFired)), MethodTargetPrefix]
    public bool Prefix_onHoldingEntityFired(ItemActionData _actionData)
    {
        if (!_actionData.invData.holdingEntity.isEntityRemote)
        {
            _actionData.invData.holdingEntity?.emodel?.avatarController.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, _actionData.indexInEntityOfAction);
            //_actionData.invData.holdingEntity?.emodel?.avatarController.CancelEvent("WeaponFire");
        }
        return true;
    }

    //[MethodTargetPostfix("onHoldingEntityFired", typeof(ItemActionRanged))]
    //public void Postfix_onHoldingEntityFired(ItemActionData _actionData, MultiActionData __customData)
    //{
    //    //after firing, if it's the last executed action then update custom accuracy
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player && MultiActionManager.GetActionIndexForEntityID(player.entityId) == _actionData.indexInEntityOfAction)
    //    {
    //        __customData.lastAccuracy = ((ItemActionRanged.ItemActionDataRanged)_actionData).lastAccuracy;
    //    }
    //}

    public static void SetAndSaveItemActionData(ItemActionData _actionData, out ItemActionData lastActionData)
    {
        lastActionData = _actionData.invData.holdingEntity.MinEventContext.ItemActionData;
        _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
    }

    public static void RestoreItemActionData(ItemActionData _actionData, ItemActionData lastActionData)
    {
        if (lastActionData != null)
            _actionData.invData.holdingEntity.MinEventContext.ItemActionData = lastActionData;
    }

    public class MultiActionData
    {
        public float lastAccuracy;
    }
}