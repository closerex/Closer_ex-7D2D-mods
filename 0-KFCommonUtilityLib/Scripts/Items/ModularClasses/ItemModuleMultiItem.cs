using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;
using static ItemModuleMultiItem;

[TypeTarget(typeof(ItemClass)), TypeDataTarget(typeof(MultiItemInvData))]
public class ItemModuleMultiItem
{
    private string boundItemName;
    private ItemClass boundItemClass;
    private ItemClass itemClass;
    public ItemClass BoundItemClass
    {
        get
        {
            if (boundItemClass == null && !string.IsNullOrEmpty(boundItemName))
            {
                boundItemClass = ItemClass.GetItemClass(boundItemName, true);
            }
            return boundItemClass;
        }
    }

    [HarmonyPatch(nameof(ItemClass.Init)), MethodTargetPostfix]
    public void Postfix_Init(ItemClass __instance)
    {
        itemClass = __instance;
        __instance.Properties.ParseString("BoundItemName", ref boundItemName);
    }

    [HarmonyPatch(nameof(ItemClass.StartHolding)), MethodTargetPrefix]
    public bool Prefix_StartHolding(ItemInventoryData _data, MultiItemInvData __customData)
    {
        var boundInvData = __customData.boundInvData;
        ItemValue boundItemValue = boundInvData.itemStack.itemValue;
        boundItemValue.Quality = _data.itemValue.Quality;
        boundItemValue.Metadata = _data.itemValue.Metadata;
        boundItemValue.Modifications = _data.itemValue.Modifications;
        boundItemValue.CosmeticMods = _data.itemValue.CosmeticMods;
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.CleanupHoldingActions)), MethodTargetPostfix]
    public void Postfix_CleanupHoldingActions(MultiItemInvData __customData)
    {
        BoundItemClass?.CleanupHoldingActions(__customData.boundInvData);
    }

    [HarmonyPatch(nameof(ItemClass.ConsumeScrollWheel)), MethodTargetPrefix]
    public bool Prefix_ConsumeScrollWheel(MultiItemInvData __customData, float _scrollWheelInput, PlayerActionsLocal _playerInput, ref bool __result)
    {
        if (__customData.useBound && BoundItemClass != null)
        {
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __result = boundItemClass.ConsumeScrollWheel(__customData.boundInvData, _scrollWheelInput, _playerInput);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.ExecuteAction)), MethodTargetPrefix]
    public bool Prefix_ExecuteAction(bool _bReleased, MultiItemInvData __customData)
    {
        if (!_bReleased)
        {
            bool flag = IsBoundActionRunning(__customData);
            return !flag;
        }
        return true;
    }

    public bool IsBoundActionRunning(MultiItemInvData __customData)
    {
        bool useBound = __customData.useBound;
        __customData.useBound = true;
        SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
        bool flag = __customData.boundInvData.item.IsActionRunning(__customData.boundInvData);
        RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
        __customData.useBound = useBound;
        return flag;
    }

    [HarmonyPatch(nameof(ItemClass.GetCameraShakeType)), MethodTargetPrefix]
    public bool Prefix_GetCameraShakeType(MultiItemInvData __customData, ref EnumCameraShake __result)
    {
        if (__customData.useBound && BoundItemClass != null)
        {
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __result = boundItemClass.GetCameraShakeType(__customData.boundInvData);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.GetFocusType)), MethodTargetPrefix]
    public bool Prefix_GetFocusType(MultiItemInvData __customData, ref RenderCubeType __result)
    {
        if (__customData.useBound && BoundItemClass != null)
        {
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __result = boundItemClass.GetFocusType(__customData.boundInvData);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.GetIronSights)), MethodTargetPrefix]
    public bool Prefix_GetIronSights(MultiItemInvData __customData, ref float _fov)
    {
        if (__customData.useBound && BoundItemClass != null)
        {
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            boundItemClass.GetIronSights(__customData.boundInvData, out _fov);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.IsActionRunning)), MethodTargetPostfix]
    public void Postfix_IsActionRunning(MultiItemInvData __customData, ref bool __result)
    {
        if (!__result && BoundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.useBound = true;
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __result |= boundItemClass.IsActionRunning(__customData.boundInvData);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __customData.useBound = useBound;
        }
    }

    [HarmonyPatch(nameof(ItemClass.IsHUDDisabled)), MethodTargetPostfix]
    public void Postfix_IsHUDDisabled(MultiItemInvData __customData, ref bool __result)
    {
        if (!__result && BoundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.useBound = true;
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __result |= boundItemClass.IsHUDDisabled(__customData.boundInvData);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __customData.useBound = useBound;
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingItemActivated)), MethodTargetPostfix]
    public void Postfix_OnHoldingItemActivated(MultiItemInvData __customData)
    {
        if (BoundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.useBound = true;
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            boundItemClass.OnHoldingItemActivated(__customData.boundInvData);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __customData.useBound = useBound;
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingReset)), MethodTargetPostfix]
    public void Postfix_OnHoldingReset(MultiItemInvData __customData)
    {
        if (BoundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.useBound = true;
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            boundItemClass.OnHoldingReset(__customData.boundInvData);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __customData.useBound = useBound;
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingUpdate)), MethodTargetPostfix]
    public void Postfix_OnHoldingUpdate(MultiItemInvData __customData)
    {
        if (BoundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.useBound = true;
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            boundItemClass.OnHoldingUpdate(__customData.boundInvData);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __customData.useBound = useBound;
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHUD)), MethodTargetPrefix]
    public bool Prefix_OnHUD(MultiItemInvData __customData, int _x, int _y)
    {
        if (__customData.useBound && BoundItemClass != null)
        {
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            boundItemClass.OnHUD(__customData.boundInvData, _x, _y);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.OnScreenOverlay)), MethodTargetPrefix]
    public bool Prefix_OnScreenOverlay(MultiItemInvData __customData)
    {
        if (__customData.useBound && BoundItemClass != null)
        {
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            boundItemClass.OnScreenOverlay(__customData.boundInvData);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(MultiItemInvData __customData, Transform _modelTransform)
    {
        if (BoundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.useBound = true;
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            boundItemClass.StartHolding(__customData.boundInvData, _modelTransform);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            __customData.useBound = useBound;
        }
    }

    [HarmonyPatch(nameof(ItemClass.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(MultiItemInvData __customData, Transform _modelTransform)
    {
        if (BoundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.useBound = true;
            SetBoundParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            boundItemClass.StopHolding(__customData.boundInvData, _modelTransform);
            RestoreParams(__customData.originalData.holdingEntity.MinEventContext, __customData);
            MultiActionManager.SetMappingForEntity(__customData.originalData.holdingEntity.entityId, null);
            __customData.useBound = useBound;
        }
    }

    public void SetBoundParams(MinEventParams param, MultiItemInvData data)
    {
        param.ItemInventoryData = data.boundInvData;
        param.ItemValue = data.boundInvData.itemStack.itemValue;
        if (data.originalData.actionData[0] is IModuleContainerFor<ActionModuleAlternative.AlternativeData> dataModule)
        {
            MultiActionManager.SetMappingForEntity(data.originalData.holdingEntity.entityId, null);
        }
    }

    public void RestoreParams(MinEventParams param, MultiItemInvData data)
    {
        param.ItemInventoryData = data.originalData;
        param.ItemValue = data.originalData.itemStack.itemValue;
        if (data.originalData.actionData[0] is IModuleContainerFor<ActionModuleAlternative.AlternativeData> dataModule)
        {
            MultiActionManager.SetMappingForEntity(data.originalData.holdingEntity.entityId, dataModule.Instance.mapping);
        }
    }

    public class MultiItemInvData
    {
        public ItemInventoryData originalData;
        public ItemInventoryData boundInvData;
        public ItemModuleMultiItem itemModule;
        public bool useBound;
        public MultiItemInvData(ItemInventoryData _invData, ItemClass _item, ItemStack _itemStack, IGameManager _gameManager, EntityAlive _holdingEntity, int _slotIdx, ItemModuleMultiItem module)
        {
            itemModule = module;
            originalData = _invData;
            if (module.BoundItemClass != null)
            {
                boundInvData = module.BoundItemClass.CreateInventoryData(new ItemStack(new ItemValue(module.BoundItemClass.Id), 1), _gameManager, _holdingEntity, _slotIdx);
            }
        }
    }
}

[HarmonyPatch]
public static class MultiItemPatches
{
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingCount), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix_holdingCount_Inventory(ItemInventoryData[] ___slots, int ___m_HoldingItemIdx, ref int __result)
    {
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound)
        {
            __result = multiInvData.Instance.boundInvData.itemStack.count;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItem), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix_holdingItem_Inventory(ItemInventoryData[] ___slots, int ___m_HoldingItemIdx, ref ItemClass __result)
    {
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound)
        {
            __result = multiInvData.Instance.boundInvData.item;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItemData), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix_holdingItemData_Inventory(ItemInventoryData[] ___slots, int ___m_HoldingItemIdx, ref ItemInventoryData __result)
    {
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound)
        {
            __result = multiInvData.Instance.boundInvData;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItemItemValue), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix_holdingItemItemValue_Inventory(ItemInventoryData[] ___slots, int ___m_HoldingItemIdx, ref ItemValue __result)
    {
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound)
        {
            __result = multiInvData.Instance.boundInvData.itemStack.itemValue;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItemStack), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix_holdingItemStack_Inventory(ItemInventoryData[] ___slots, int ___m_HoldingItemIdx, ref ItemStack __result)
    {
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound)
        {
            __result = multiInvData.Instance.boundInvData.itemStack;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Inventory), "Item", MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix_Item_Inventory(ItemInventoryData[] ___slots, int _idx, ref ItemValue __result)
    {
        if (___slots[_idx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound)
        {
            __result = multiInvData.Instance.boundInvData.itemStack.itemValue;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SwapSelectedAmmo))]
    [HarmonyPrefix]
    private static bool Prefix_SwapSelectedAmmo_ItemActionRanged(EntityAlive _entity, int _ammoIndex)
    {
        if (_entity.inventory?.holdingItemData is IModuleContainerFor<MultiItemInvData> dataModule && dataModule.Instance.itemModule.IsBoundActionRunning(dataModule.Instance))
        {
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMoveController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            //ItemAction
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 37)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(MultiItemPatches), nameof(CheckAltMelee))
                });
                break;
            }
        }

        return codes;
    }

    private static void CheckAltMelee(PlayerMoveController controller)
    {
        if (DroneManager.Debug_LocalControl || !controller.gameManager.gameStateManager.IsGameStarted() || GameStats.GetInt(EnumGameStats.GameState) != 1)
            return;

        bool isUIOpen = controller.windowManager.IsCursorWindowOpen() || controller.windowManager.IsInputActive() || controller.windowManager.IsModalWindowOpen();
        if (isUIOpen || controller.entityPlayerLocal.emodel.IsRagdollActive || controller.entityPlayerLocal.IsDead() || controller.entityPlayerLocal.AttachedToEntity != null)
        {
            return;
        }

        EntityPlayerLocal player = controller.entityPlayerLocal;
        bool wasPressed = PlayerActionKFLib.Instance.AltMelee.WasPressed;
        bool wasReleased = PlayerActionKFLib.Instance.AltMelee.WasReleased;
        if (PlayerActionKFLib.Instance.Enabled && player.inventory.GetIsFinishedSwitchingHeldItem() && (wasPressed || wasReleased) && player.inventory.holdingItemData is IModuleContainerFor<MultiItemInvData> dataModule)
        {
            bool isReloading = player.IsReloading();
            int actionIndex = MultiActionManager.GetActionIndexForEntity(player);
            var reloadModule = player.inventory.holdingItem.Actions[actionIndex] as IModuleContainerFor<ActionModuleInterruptReload>;
            MultiItemInvData multiInvData = dataModule.Instance;
            if (multiInvData.itemModule.BoundItemClass?.Actions?[0] is ItemActionDynamicMelee dynamicAction && !multiInvData.itemModule.IsBoundActionRunning(multiInvData) && ((player.inventory.IsHoldingGun() && (!isReloading || reloadModule != null)) || !player.inventory.IsHoldingItemActionRunning()) && !player.AimingGun && !multiInvData.originalData.IsAnyActionLocked())
            {
                if (wasPressed)
                {
                    multiInvData.useBound = true;
                    multiInvData.itemModule.SetBoundParams(player.MinEventContext, multiInvData);
                    ItemActionDynamicMelee.ItemActionDynamicMeleeData meleeData = multiInvData.boundInvData.actionData[0] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
                    bool canRun = dynamicAction.canStartAttack(meleeData);
                    multiInvData.itemModule.RestoreParams(player.MinEventContext, multiInvData);
                    multiInvData.useBound = false;
                    if (!canRun)
                    {
                        //Log.Out($"Fail to run alt melee on slot {player.inventory.holdingItemIdx} released {wasReleased} is switching item {player.inventory.isSwitchingHeldItem} is attacking {meleeData.Attacking} execute time elapsed {Time.time - meleeData.lastUseTime}");
                        return;
                    }
                    if (isReloading)
                    {
                        var itemActionData = player.inventory.holdingItemData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
                        itemActionData.m_LastShotTime = Time.time;
                        reloadModule.Instance.Postfix_ExecuteAction(itemActionData, ((IModuleContainerFor<ActionModuleInterruptReload.InterruptData>)itemActionData).Instance, new ActionModuleInterruptReload.State
                        {
                            executed = true,
                            lastShotTime = -1,
                            isReloading = true,
                            isWeaponReloading = true,
                        });
                    }
                    player.emodel.avatarController.UpdateBool(AvatarController.reloadHash, false);
                    multiInvData.boundInvData.itemStack.itemValue.UseTimes = 0;
                }

                multiInvData.useBound = true;
                multiInvData.itemModule.SetBoundParams(player.MinEventContext, multiInvData);
                multiInvData.itemModule.BoundItemClass.ExecuteAction(0, multiInvData.boundInvData, wasReleased, controller.playerInput);
                player.emodel.avatarController.UpdateBool("UseAltMelee", true);
                multiInvData.itemModule.RestoreParams(player.MinEventContext, multiInvData);
                multiInvData.useBound = false;
                //Log.Out($"Execute alt melee on slot {player.inventory.holdingItemIdx} released {wasReleased}");
            }
        }
    }

    //[HarmonyPatch(typeof(Animator), nameof(Animator.ResetTrigger), new[] {typeof(string)})]
    //[HarmonyPrefix]
    //private static void Prefix(Animator __instance, string name)
    //{
    //    if (name == "PowerAttack" && __instance.GetBool(name))
    //    {
    //        Log.Warning($"RESETTING POWER ATTACK TRIGGER\n{StackTraceUtility.ExtractStackTrace()}");
    //    }
    //}
}