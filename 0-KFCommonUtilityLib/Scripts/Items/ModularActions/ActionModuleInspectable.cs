using GearsAPI.Settings.Global;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using UnityEngine;

public static class InspectSettings
{
    public static bool defaultAutoInspect = true;
    public static float autoInspectInterval = 30;
    private const float MinInspectInterval = 30;
    private const float MaxInspectInterval = 60;

    public static void InitSettings(IModGlobalSettings modSettings)
    {
        var category = modSettings.GetTab("MiscSettings").GetCategory("AutoInspect");

        var enableSetting = category.GetSetting("EnableAutoInspect") as ISwitchGlobalSetting;
        defaultAutoInspect = enableSetting.CurrentValue == "Enabled";
        enableSetting.OnSettingChanged += static (s, v) => defaultAutoInspect = v == "Enabled";

        var intervalSetting = category.GetSetting("AutoInspectInterval") as ISliderGlobalSetting;
        autoInspectInterval = Mathf.Clamp(float.Parse(intervalSetting.CurrentValue), MinInspectInterval, MaxInspectInterval);
        intervalSetting.OnSettingChanged += static (s, v) =>
        {
            if (float.TryParse(v, out var val))
            {
                autoInspectInterval = Mathf.Clamp(val, MinInspectInterval, MaxInspectInterval);
            }
        };
    }
}

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(InspectableData))]
public class ActionModuleInspectable
{
    public bool allowEmptyInspect;
    public bool autoInspect;

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        allowEmptyInspect = _props.GetBool("allowEmptyInspect");
        autoInspect = true;
        _props.ParseBool("autoInspect", ref autoInspect);
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemActionData _data, InspectableData __customData)
    {
        __customData.lastInspectTime = Time.time;
        var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(_data.invData.holdingEntity);
        __customData.inspectAvailable = targets && targets.IsAnimationSet;
    }

    [HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemAction.CancelAction)), MethodTargetPostfix]
    public void Postfix_ItemActionDynamic_CancelAction(ItemActionDynamic.ItemActionDynamicData _actionData, InspectableData __customData)
    {
        var entity = _actionData.invData.holdingEntity;
        if (!entity.MovementRunning && !__customData.invData.item.IsActionRunning(__customData.invData))
        {
            __customData.TriggerInspect();
        }
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPostfix]
    public void Postfix_ExecuteAction(InspectableData __customData)
    {
        __customData.lastInspectTime = Time.time;
    }

    [HarmonyPatch(nameof(ItemAction.ItemActionEffects)), MethodTargetPostfix]
    public void Postfix_ItemActionEffects(InspectableData __customData)
    {
        __customData.lastInspectTime = Time.time;
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    public void Postfix_OnHoldingUpdate(ItemAction __instance, ItemActionData _actionData, InspectableData __customData)
    {
        if (!autoInspect || !InspectSettings.defaultAutoInspect || _actionData.indexInEntityOfAction != 0)
        {
            __customData.lastInspectTime = Time.time;
            return;
        }

        if (__customData.CanInspect() && !_actionData.invData.holdingEntity.IsCrouching)
        {
            if (Time.time - __customData.lastInspectTime >= InspectSettings.autoInspectInterval)
            {
                __customData.TriggerInspect();
            }
        }
        else
        {
            __customData.lastInspectTime = Time.time;
        }
    }

    public class InspectableData
    {
        public float lastInspectTime;
        public bool inspectAvailable;
        public ActionModuleInspectable module;
        public ItemInventoryData invData;

        public static int weaponInspectHash = Animator.StringToHash("weaponInspect");
        public static int altInspectHash = Animator.StringToHash("altInspect");

        public InspectableData(ItemInventoryData _inventoryData, ActionModuleInspectable __customModule)
        {
            this.module = __customModule;
            this.invData = _inventoryData;
        }

        public bool CanInspect()
        {
            if (invData == null || !inspectAvailable)
            {
                return false;
            }
            var player = invData.holdingEntity as EntityPlayerLocal;
            return player && !player.movementInput.running && !player.AimingGun && !player.bLerpCameraFlag && !invData.item.IsActionRunning(invData) && (invData.itemValue.Meta > 0 || module.allowEmptyInspect);
        }

        public void TriggerInspect(bool useAlt = false)
        {
            invData.holdingEntity.emodel.avatarController._setTrigger(weaponInspectHash, true);
            invData.holdingEntity.emodel.avatarController._setBool(altInspectHash, useAlt);
            lastInspectTime = Time.time;
        }
    }
}

[HarmonyPatch]
public static class ActionModuleInspectablePatches
{
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SwapSelectedAmmo))]
    [HarmonyPrefix]
    private static bool Prefix_SwapSelectedAmmo_ItemActionRanged(ItemActionRanged __instance, EntityAlive _entity, int _ammoIndex)
    {
        if (_ammoIndex == (int)_entity.inventory.holdingItemItemValue.SelectedAmmoTypeIndex && _entity is EntityPlayerLocal player)
        {
            ItemActionRanged.ItemActionDataRanged _actionData = _entity.inventory.holdingItemData.actionData[__instance.ActionIndex] as ItemActionRanged.ItemActionDataRanged;
            if (!__instance.CanReload(_actionData) && _actionData is IModuleContainerFor<ActionModuleInspectable.InspectableData> dataModule && dataModule.Instance.CanInspect())
            {
                dataModule.Instance.TriggerInspect();
                return false;
            }
        }
        return true;
    }


    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyPrefix]
    private static bool Prefix_Update_PlayerMoveController(PlayerMoveController __instance)
    {
        if (DroneManager.Debug_LocalControl || !__instance.gameManager.gameStateManager.IsGameStarted() || GameStats.GetInt(EnumGameStats.GameState) != 1)
            return true;

        bool isUIOpen = __instance.windowManager.IsCursorWindowOpen() || __instance.windowManager.IsInputActive() || __instance.windowManager.IsModalWindowOpen();

        UpdateLocalInput(__instance.entityPlayerLocal, isUIOpen);

        return true;
    }

    private static void UpdateLocalInput(EntityPlayerLocal _player, bool _isUIOpen)
    {
        if (_isUIOpen || _player.emodel.IsRagdollActive || _player.IsDead() || _player.AttachedToEntity != null)
        {
            return;
        }

        if (PlayerActionKFLib.Instance.Enabled && PlayerActionKFLib.Instance.AltInspect.WasPressed)
        {
            var actionData = _player.inventory.holdingItemData.actionData[MultiActionManager.GetActionIndexForEntity(_player)];
            if (actionData is IModuleContainerFor<ActionModuleInspectable.InspectableData> inspectableData && inspectableData.Instance.CanInspect())
            {
                inspectableData.Instance.TriggerInspect(true);
            }
        }
    }
}
