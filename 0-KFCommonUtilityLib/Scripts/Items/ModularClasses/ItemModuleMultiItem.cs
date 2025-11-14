using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;
using static ItemModuleMultiItem;

[TypeTarget(typeof(ItemClass)), TypeDataTarget(typeof(MultiItemInvData))]
public class ItemModuleMultiItem
{
    //private ItemClass itemClass;

    //[HarmonyPatch(nameof(ItemClass.Init)), MethodTargetPostfix]
    //public void Postfix_Init(ItemClass __instance)
    //{
    //    itemClass = __instance;
    //    __instance.Properties.ParseString("BoundItemName", ref boundItemName);
    //}

    [HarmonyPatch(nameof(ItemClass.StartHolding)), MethodTargetPrefix]
    public bool Prefix_StartHolding(ItemClass __instance, ItemInventoryData _data, MultiItemInvData __customData)
    {
        __instance.Properties.ParseString("BoundItemName", ref __customData.boundItemName);
        __customData.boundItemName = _data.itemValue.GetPropertyOverride("BoundItemName", __customData.boundItemName);
        __customData.UpdateBoundItem();
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.CleanupHoldingActions)), MethodTargetPostfix]
    public void Postfix_CleanupHoldingActions(MultiItemInvData __customData)
    {
        __customData.boundItemClass?.CleanupHoldingActions(__customData.boundInvData);
    }

    [HarmonyPatch(nameof(ItemClass.ConsumeScrollWheel)), MethodTargetPrefix]
    public bool Prefix_ConsumeScrollWheel(MultiItemInvData __customData, float _scrollWheelInput, PlayerActionsLocal _playerInput, ref bool __result)
    {
        if (__customData.useBound && __customData.boundItemClass != null)
        {
            __customData.SetBoundParams();
            __result = __customData.boundItemClass.ConsumeScrollWheel(__customData.boundInvData, _scrollWheelInput, _playerInput);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.ExecuteAction)), MethodTargetPrefix]
    public bool Prefix_ExecuteAction(ItemInventoryData _data, bool _bReleased, PlayerActionsLocal _playerActions)
    {
        if (!_bReleased && _playerActions != null)
            _data.holdingEntity.emodel.avatarController.UpdateBool("UseAltMelee", false);
        return true;
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemClass_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var lbl = generator.DefineLabel();
        codes[0].WithLabels(lbl);

        codes.InsertRange(0, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_3),
            new CodeInstruction(OpCodes.Brtrue_S, lbl),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<MultiItemInvData>)),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<MultiItemInvData>), nameof(IModuleContainerFor<MultiItemInvData>.Instance))),
            CodeInstruction.Call(typeof(MultiItemInvData), nameof(MultiItemInvData.IsBoundActionRunning)),
            new CodeInstruction(OpCodes.Brfalse_S, lbl),
            new CodeInstruction(OpCodes.Ret)
        });

        return codes;
    }

    [HarmonyPatch(nameof(ItemClass.GetCameraShakeType)), MethodTargetPrefix]
    public bool Prefix_GetCameraShakeType(MultiItemInvData __customData, ref EnumCameraShake __result)
    {
        if (__customData.useBound && __customData.boundItemClass != null)
        {
            __customData.SetBoundParams();
            __result = __customData.boundItemClass.GetCameraShakeType(__customData.boundInvData);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.GetFocusType)), MethodTargetPrefix]
    public bool Prefix_GetFocusType(MultiItemInvData __customData, ref RenderCubeType __result)
    {
        if (__customData.useBound && __customData.boundItemClass != null)
        {
            __customData.SetBoundParams();
            __result = __customData.boundItemClass.GetFocusType(__customData.boundInvData);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.GetIronSights)), MethodTargetPrefix]
    public bool Prefix_GetIronSights(MultiItemInvData __customData, ref float _fov)
    {
        if (__customData.useBound && __customData.boundItemClass != null)
        {
            __customData.SetBoundParams();
            __customData.boundItemClass.GetIronSights(__customData.boundInvData, out _fov);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.IsActionRunning)), MethodTargetPostfix]
    public void Postfix_IsActionRunning(MultiItemInvData __customData, ref bool __result)
    {
        if (!__result && __customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            __result |= __customData.boundItemClass.IsActionRunning(__customData.boundInvData);
            __customData.RestoreParams(useBound);
        }
    }

    [HarmonyPatch(nameof(ItemClass.IsHUDDisabled)), MethodTargetPostfix]
    public void Postfix_IsHUDDisabled(MultiItemInvData __customData, ref bool __result)
    {
        if (!__result && __customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            __result |= __customData.boundItemClass.IsHUDDisabled(__customData.boundInvData);
            __customData.RestoreParams(useBound);
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingItemActivated)), MethodTargetPostfix]
    public void Postfix_OnHoldingItemActivated(MultiItemInvData __customData)
    {
        if (__customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            __customData.boundItemClass.OnHoldingItemActivated(__customData.boundInvData);
            __customData.RestoreParams(useBound);
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingReset)), MethodTargetPostfix]
    public void Postfix_OnHoldingReset(MultiItemInvData __customData)
    {
        if (__customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            __customData.boundItemClass.OnHoldingReset(__customData.boundInvData);
            __customData.RestoreParams(useBound);
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingUpdate)), MethodTargetPostfix]
    public void Postfix_OnHoldingUpdate(MultiItemInvData __customData)
    {
        if (__customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            __customData.boundItemClass.OnHoldingUpdate(__customData.boundInvData);
            __customData.RestoreParams(useBound);
        }
    }

    [HarmonyPatch(nameof(ItemClass.OnHUD)), MethodTargetPrefix]
    public bool Prefix_OnHUD(MultiItemInvData __customData, int _x, int _y)
    {
        if (__customData.useBound && __customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            __customData.boundItemClass.OnHUD(__customData.boundInvData, _x, _y);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.OnScreenOverlay)), MethodTargetPrefix]
    public bool Prefix_OnScreenOverlay(MultiItemInvData __customData)
    {
        if (__customData.useBound && __customData.boundItemClass != null)
        {
            __customData.SetBoundParams();
            __customData.boundItemClass.OnScreenOverlay(__customData.boundInvData);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(MultiItemInvData __customData, Transform _modelTransform)
    {
        if (__customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            __customData.boundItemClass.StartHolding(__customData.boundInvData, _modelTransform);
            __customData.RestoreParams(useBound);
        }
    }

    [HarmonyPatch(nameof(ItemClass.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(MultiItemInvData __customData, Transform _modelTransform)
    {
        if (__customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            __customData.boundItemClass.StopHolding(__customData.boundInvData, _modelTransform);
            __customData.RestoreParams(useBound);
            MultiActionManager.SetMappingForEntity(__customData.originalData.holdingEntity.entityId, null);
        }
    }

    [HarmonyPatch(typeof(ItemClassExtendedFunction), nameof(ItemClassExtendedFunction.CancelAllActions)), MethodTargetPostfix]
    public void Postfix_CancelAllActions(ItemClass __instance, ItemInventoryData _invData, MultiItemInvData __customData)
    {
        if (__customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            (__customData.boundItemClass as ItemClassExtendedFunction)?.CancelAllActions(__customData.boundInvData);
            __customData.RestoreParams(useBound);
        }
    }

    [HarmonyPatch(typeof(ItemClassExtendedFunction), nameof(ItemClassExtendedFunction.GetAllExecutingActions)), MethodTargetPostfix]
    public void Postfix_GetAllExecutingActions(ItemClass __instance, ItemInventoryData _invData, List<ItemAction> _actionList, List<ItemActionData> _dataList, MultiItemInvData __customData)
    {
        if (__customData.boundItemClass != null)
        {
            bool useBound = __customData.useBound;
            __customData.SetBoundParams();
            (__customData.boundItemClass as ItemClassExtendedFunction)?.GetAllExecutingActions(__customData.boundInvData, _actionList, _dataList);
            __customData.RestoreParams(useBound);
        }
    }


    private static List<ItemAction> tempActionList = new List<ItemAction>();
    private static List<ItemActionData> tempDataList = new List<ItemActionData>();
    public static bool CheckAltMelee(EntityPlayerLocal player, MultiItemInvData multiInvData, bool bReleased, int meleeActionIndex = 0, bool useAltParam = true)
    {
        if (!player.inventory.GetIsFinishedSwitchingHeldItem())
        {
            return false;
        }
        ItemActionDynamicMelee dynamicAction = multiInvData.boundItemClass?.Actions?[meleeActionIndex] as ItemActionDynamicMelee;
        if (dynamicAction == null)
        {
            return false;
        }
        ItemActionDynamicMelee.ItemActionDynamicMeleeData meleeData = multiInvData.boundInvData.actionData[meleeActionIndex] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
        if (meleeData == null)
        {
            return false;
        }

        if (player.inventory.holdingItemData.itemValue.PercentUsesLeft <= 0)
        {
            return false;
        }
        //if (!bReleased)
        //{
        //    multiInvData.SetBoundParams();
        //    bool isTargetActionRunning = dynamicAction.IsActionRunning(meleeData);
        //    multiInvData.RestoreParams(false);
        //    if (isTargetActionRunning)
        //    {
        //        return false;
        //    }
        //}

        bool isActionRunning = false;
        bool isAllRunningActionInterruptable = false;
        var interruptData = (meleeData as IModuleContainerFor<ActionModuleAnimationInterruptSource.AnimationInterruptSourceData>)?.Instance;
        if (!bReleased)
        {
            tempActionList.Clear();
            tempDataList.Clear();
            ActionModuleAnimationInterruptSource.GetAllRunningAndInterruptableActions(player, dynamicAction, meleeData, tempActionList, tempDataList, out isActionRunning, out isAllRunningActionInterruptable);
        }

        bool isReloading = player.IsReloading();
        int actionIndex = MultiActionManager.GetActionIndexForEntity(player);
        var reloadModule = player.inventory.holdingItem.Actions[actionIndex] as IModuleContainerFor<ActionModuleInterruptReload>;

        bool isActionOrReloadingInterruptable = false;
        if (isActionRunning)
        {
            if (isReloading && reloadModule != null)
            {
                isActionOrReloadingInterruptable = true;
            }
            else if (isAllRunningActionInterruptable)
            {
                isActionOrReloadingInterruptable = true;
            }
        }
        else
        {
            isActionOrReloadingInterruptable = true;
        }

        if ((bReleased || isActionOrReloadingInterruptable) && !player.AimingGun && !multiInvData.originalData.IsAnyActionLocked())
        {
            if (ConsoleCmdReloadLog.LogInfo)
            {
                Log.Out($"bReleased {bReleased} isActionRunning {isActionRunning} isAllRunningActionInterruptable {isAllRunningActionInterruptable} isReloading {isReloading} isActionOrReloadingInterruptable {isActionOrReloadingInterruptable}");
            }
            if (!bReleased)
            {
                multiInvData.SetBoundParams();
                bool canRun = dynamicAction.canStartAttack(meleeData);
                multiInvData.RestoreParams(false);
                if (!canRun)
                {
                    Log.Out($"Fail to run alt melee on slot {player.inventory.holdingItemIdx} action{actionIndex} released {bReleased} is switching item {player.inventory.isSwitchingHeldItem} is attacking {meleeData.Attacking} execute time elapsed {Time.time - meleeData.lastUseTime}");
                    return false;
                }
                if (isReloading)
                {
                    var itemActionData = player.inventory.holdingItemData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
                    itemActionData.m_LastShotTime = 0;
                    reloadModule.Instance.Postfix_ExecuteAction(itemActionData, ((IModuleContainerFor<ActionModuleInterruptReload.InterruptData>)itemActionData).Instance, new ActionModuleInterruptReload.State
                    {
                        executed = true,
                        lastShotTime = -1,
                        isReloading = true,
                        isWeaponReloading = true,
                    });
                }
                if (isActionRunning && isAllRunningActionInterruptable)
                {
                    (player.inventory.holdingItem as ItemClassExtendedFunction)?.CancelAllActions(player.inventory.holdingItemData);
                    if (interruptData != null)
                    {
                        interruptData.interrupted = true;
                    }
                }
                player.emodel.avatarController.UpdateBool(AvatarController.reloadHash, false);
                multiInvData.boundInvData.itemStack.itemValue.UseTimes = 0;
                player.emodel.avatarController.UpdateBool("UseAltMelee", useAltParam);
            }
            else if (interruptData != null)
            {
                interruptData.interrupted = false;
            }

            multiInvData.SetBoundParams();
            multiInvData.boundItemClass.ExecuteAction(meleeActionIndex, multiInvData.boundInvData, bReleased, null);
            multiInvData.RestoreParams(false);
            return true;
            //Log.Out($"Execute alt melee on slot {player.inventory.holdingItemIdx} released {wasReleased}");
        }

        if (bReleased && interruptData != null)
        {
            interruptData.interrupted = false;
        }
        return false;
    }

    public class MultiItemInvData
    {
        public bool useBound;
        public ItemInventoryData originalData;
        public ItemInventoryData boundInvData;
        public ItemModuleMultiItem itemModule;
        public string boundItemName;
        public ItemClass boundItemClass;

        public MultiItemInvData(ItemInventoryData __instance, ItemModuleMultiItem __customModule)
        {
            itemModule = __customModule;
            originalData = __instance;
            boundInvData = null;
            //if (module.boundItemClass != null)
            //{
            //    boundInvData = module.boundItemClass.CreateInventoryData(new ItemStack(new ItemValue(module.boundItemClass.Id), 1), _gameManager, _holdingEntity, _slotIdx);
            //}
        }

        public void UpdateBoundItem()
        {
            if (boundItemClass == null || boundItemClass.Name != boundItemName)
            {
                boundItemClass = ItemClass.GetItemClass(boundItemName, true);
                if (boundItemClass != null)
                {
                    boundInvData = boundItemClass.CreateInventoryData(new ItemStack(new ItemValue(boundItemClass.Id, originalData.itemStack.itemValue.Quality, originalData.itemStack.itemValue.Quality), 1), originalData.gameManager, originalData.holdingEntity, originalData.slotIdx);
                    SyncBoundItemValue();
                }
                else
                {
                    boundInvData = null;
                }
            }
        }

        private void SyncBoundItemValue()
        {
            ItemValue boundItemValue = boundInvData.itemStack.itemValue;
            ItemValue originalItemValue = originalData.itemStack.itemValue;
            boundItemValue.Quality = originalItemValue.Quality;
            boundItemValue.Metadata = originalItemValue.Metadata;
            boundItemValue.Modifications = originalItemValue.Modifications;
            boundItemValue.CosmeticMods = originalItemValue.CosmeticMods;
            boundItemValue.Meta = originalItemValue.Meta;
            if (originalItemValue.Metadata == null)
            {
                originalItemValue.Metadata = new();
            }
            boundItemValue.Metadata = originalItemValue.Metadata;

        }

        public bool IsBoundActionRunning()
        {
            if (boundInvData == null)
            {
                return false;
            }
            bool useBound = this.useBound;
            SetBoundParams();
            bool flag = boundInvData.item.IsActionRunning(boundInvData);
            RestoreParams(useBound);
            return flag;
        }

        public void SetBoundParams()
        {
            if (boundInvData == null)
            {
                return;
            }
            useBound = true;
            var param = originalData.holdingEntity.MinEventContext;
            param.ItemInventoryData = boundInvData;
            param.ItemValue = boundInvData.itemStack.itemValue;
            boundInvData.itemStack.itemValue.Meta = originalData.itemStack.itemValue.Meta;
            boundInvData.itemStack.itemValue.Metadata = originalData.itemStack.itemValue.Metadata;
            boundInvData.itemStack.itemValue.Activated = originalData.itemStack.itemValue.Activated;
            boundInvData.itemStack.itemValue.UseTimes = 0;
            if (originalData.actionData[0] is IModuleContainerFor<ActionModuleAlternative.AlternativeData> dataModule)
            {
                MultiActionManager.SetMappingForEntity(originalData.holdingEntity.entityId, null);
            }
        }

        public void RestoreParams(bool prevUseBound)
        {
            if (boundInvData == null)
            {
                return;
            }
            this.useBound = prevUseBound;
            var param = originalData.holdingEntity.MinEventContext;
            param.ItemInventoryData = originalData;
            param.ItemValue = originalData.itemStack.itemValue;
            boundInvData.itemStack.itemValue.Meta = originalData.itemStack.itemValue.Meta;
            if (originalData.actionData[0] is IModuleContainerFor<ActionModuleAlternative.AlternativeData> dataModule)
            {
                MultiActionManager.SetMappingForEntity(originalData.holdingEntity.entityId, dataModule.Instance.mapping);
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
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound && multiInvData.Instance.boundInvData != null)
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
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound && multiInvData.Instance.boundInvData != null)
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
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound && multiInvData.Instance.boundInvData != null)
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
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound && multiInvData.Instance.boundInvData != null)
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
        if (___slots[___m_HoldingItemIdx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound && multiInvData.Instance.boundInvData != null)
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
        if (___slots[_idx] is IModuleContainerFor<MultiItemInvData> multiInvData && multiInvData.Instance.useBound && multiInvData.Instance.boundInvData != null)
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
        if (_entity.inventory?.holdingItemData is IModuleContainerFor<MultiItemInvData> dataModule && dataModule.Instance.IsBoundActionRunning())
        {
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.IsAimingGunPossible))]
    [HarmonyPostfix]
    private static void Postfix_IsAimingGunPossible_EntityPlayerLocal(EntityPlayerLocal __instance, ref bool __result)
    {
        if (__result && __instance.inventory?.holdingItemData is IModuleContainerFor<MultiItemInvData> dataModule)
        {
            var multiItemData = dataModule.Instance;
            if (multiItemData.boundInvData != null)
            {
                bool useBound = multiItemData.useBound;
                var prevData = __instance.MinEventContext.ItemActionData;
                multiItemData.SetBoundParams();
                for (int i = 0; i < multiItemData.boundInvData.actionData.Count; i++)
                {
                    var action = multiItemData.boundItemClass.Actions[i];
                    var actionData = multiItemData.boundInvData.actionData[i];
                    if (action != null && !action.IsAimingGunPossible(actionData))
                    {
                        __result = false;
                        break;
                    }
                }
                multiItemData.RestoreParams(useBound);
                __instance.MinEventContext.ItemActionData = prevData;
            }
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.CanReload))]
    [HarmonyPostfix]
    private static void Postfix_CanReload_ItemActionRanged(ItemActionRanged __instance, ItemActionRanged.ItemActionDataRanged _actionData, ref bool __result)
    {
        if (__result)
        {
            EntityAlive holdingEntity = _actionData.invData.holdingEntity;
            if (holdingEntity.inventory.holdingItemData is IModuleContainerFor<MultiItemInvData> dataModule)
            {
                var multiItemData = dataModule.Instance;
                if (multiItemData.boundInvData != null)
                {
                    __result = !multiItemData.IsBoundActionRunning();
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMoveController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        int localIndex;
        if (Constants.cVersionInformation.Major <= 2 && Constants.cVersionInformation.Minor <= 1)
        {
            localIndex = 35;
        }
        else
        {
            localIndex = 37;
        }

        for (int i = 0; i < codes.Count; i++)
        {
            //ItemAction
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == localIndex)
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

    public static void CheckAltMelee(PlayerMoveController controller)
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

        if (wasPressed && (controller.playerInput.Primary.IsPressed || controller.playerInput.Secondary.IsPressed))
        {
            return;
        }

        if (PlayerActionKFLib.Instance.Enabled && (player.inventory.GetIsFinishedSwitchingHeldItem() || wasReleased) && (wasPressed || wasReleased) && player.inventory.holdingItemData is IModuleContainerFor<MultiItemInvData> dataModule)
        {
            ItemModuleMultiItem.CheckAltMelee(player, dataModule.Instance, wasReleased);
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