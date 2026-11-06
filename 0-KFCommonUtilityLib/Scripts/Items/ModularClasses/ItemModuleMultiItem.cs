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
    public void Prefix_StartHolding(ItemClass __instance, ItemInventoryData _data, MultiItemInvData __customData)
    {
        __instance.Properties.ParseString("BoundItemName", ref __customData.boundItemName);
        __customData.boundItemName = _data.itemValue.GetPropertyOverride("BoundItemName", __customData.boundItemName);
        __customData.UpdateBoundItem();
    }

    [HarmonyPatch(nameof(ItemClass.CleanupHoldingActions)), MethodTargetPostfix]
    public void Postfix_CleanupHoldingActions(MultiItemInvData __customData)
    {
        __customData.boundItemClass?.CleanupHoldingActions(__customData.boundInvData);
    }

    [HarmonyPatch(nameof(ItemClass.ConsumeScrollWheel)), MethodTargetPrefix]
    public bool Prefix_ConsumeScrollWheel(MultiItemInvData __customData, float _scrollWheelInput, PlayerActionsLocal _playerInput, bool __runOriginal, ref bool __result)
    {
        if (__runOriginal && __customData.useBound && __customData.boundItemClass != null)
        {
            __customData.SetBoundParams();
            __result = __customData.boundItemClass.ConsumeScrollWheel(__customData.boundInvData, _scrollWheelInput, _playerInput);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.ExecuteAction)), MethodTargetPrefix]
    public void Prefix_ExecuteAction(ItemInventoryData _data, bool _bReleased, PlayerActionsLocal _playerActions)
    {
        if (!_bReleased && _playerActions != null)
            _data.holdingEntity.emodel.avatarController.UpdateBool("UseAltMelee", false);
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
            CodeInstruction.Call(typeof(ItemClassModuleProcessor), nameof(ItemClassModuleProcessor.LoadDataModule), null, new []{typeof(MultiItemInvData)}),
            CodeInstruction.Call(typeof(MultiItemInvData), nameof(MultiItemInvData.IsBoundActionRunning)),
            new CodeInstruction(OpCodes.Brfalse_S, lbl),
            new CodeInstruction(OpCodes.Ret)
        });

        return codes;
    }

    [HarmonyPatch(nameof(ItemClass.GetCameraShakeType)), MethodTargetPrefix]
    public bool Prefix_GetCameraShakeType(MultiItemInvData __customData, ref EnumCameraShake __result, bool __runOriginal)
    {
        if (__runOriginal && __customData.useBound && __customData.boundItemClass != null)
        {
            __customData.SetBoundParams();
            __result = __customData.boundItemClass.GetCameraShakeType(__customData.boundInvData);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.GetFocusType)), MethodTargetPrefix]
    public bool Prefix_GetFocusType(MultiItemInvData __customData, ref RenderCubeType __result, bool __runOriginal)
    {
        if (__runOriginal && __customData.useBound && __customData.boundItemClass != null)
        {
            __customData.SetBoundParams();
            __result = __customData.boundItemClass.GetFocusType(__customData.boundInvData);
            __customData.RestoreParams(true);
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.GetIronSights)), MethodTargetPrefix]
    public bool Prefix_GetIronSights(MultiItemInvData __customData, ref float _fov, bool __runOriginal)
    {
        if (__runOriginal && __customData.useBound && __customData.boundItemClass != null)
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
    public bool Prefix_OnHUD(MultiItemInvData __customData, int _x, int _y, bool __runOriginal)
    {
        if (__runOriginal && __customData.useBound && __customData.boundItemClass != null)
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
    public bool Prefix_OnScreenOverlay(MultiItemInvData __customData, bool __runOriginal)
    {
        if (__runOriginal && __customData.useBound && __customData.boundItemClass != null)
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


    private static List<ItemAction> tempActionList = new();
    private static List<ItemActionData> tempDataList = new();
    public static bool CheckAltMelee(EntityPlayerLocal player, MultiItemInvData multiInvData, bool bReleased, int meleeActionIndex = 0, bool useAltParam = true)
    {
        if (player.Hand.IsSwitching)
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

        if (multiInvData.originalData.itemValue.PercentUsesLeft <= 0)
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
        var interruptData = meleeData.LoadModule<ActionModuleAnimationInterruptSource.AnimationInterruptSourceData>();
        if (!bReleased)
        {
            tempActionList.Clear();
            tempDataList.Clear();
            ActionModuleAnimationInterruptSource.GetAllRunningAndInterruptableActions(player, dynamicAction, meleeData, tempActionList, tempDataList, out isActionRunning, out isAllRunningActionInterruptable);
        }

        bool isReloading = player.IsReloading();
        int actionIndex = MultiActionManager.GetActionIndexForEntity(player);
        var reloadModule = multiInvData.originalData.item.Actions[actionIndex].LoadModule<ActionModuleInterruptReload>();

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
                    Log.Out($"Fail to run alt melee on slot {multiInvData.originalData.slotIdx} action{actionIndex} released {bReleased} is switching item {player.Hand.IsSwitching} is attacking {meleeData.Attacking} execute time elapsed {Time.time - meleeData.lastUseTime}");
                    return false;
                }
                if (isReloading)
                {
                    var itemActionData = multiInvData.originalData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
                    itemActionData.m_LastShotTime = 0;
                    reloadModule.Postfix_ExecuteAction(itemActionData, itemActionData.LoadModule<ActionModuleInterruptReload.InterruptData>(), new ActionModuleInterruptReload.State
                    {
                        executed = true,
                        lastShotTime = -1,
                        isReloading = true,
                        isWeaponReloading = true,
                    });
                }
                if (isActionRunning && isAllRunningActionInterruptable)
                {
                    (multiInvData.originalData.item as ItemClassExtendedFunction)?.CancelAllActions(multiInvData.originalData);
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
            //Log.Out($"Execute alt melee on slot {multiInvData.originalData.slotIdx} released {wasReleased}");
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
        public readonly ItemInventoryData originalData;
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
                    boundInvData = ItemInventoryData.Create(originalData.holdingEntity, new ItemStack(new ItemValue(boundItemClass.Id, originalData.itemValue.Quality, originalData.itemValue.Quality), 1), originalData.slotIdx);
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
            boundItemValue.Stats = originalItemValue.Stats;
            boundItemValue.Metadata = originalItemValue.Metadata;
            boundItemValue.modifications = originalItemValue.modifications;
            boundItemValue.cosmeticMods = originalItemValue.cosmeticMods;
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
            boundInvData.itemStack.itemValue.Stats = originalData.itemStack.itemValue.Stats;
            boundInvData.itemStack.itemValue.Meta = originalData.itemStack.itemValue.Meta;
            boundInvData.itemStack.itemValue.Metadata = originalData.itemStack.itemValue.Metadata;
            boundInvData.itemStack.itemValue.Activated = originalData.itemStack.itemValue.Activated;
            boundInvData.itemStack.itemValue.UseTimes = 0;
            if (originalData.actionData[0].IsModuleContainerFor(out ActionModuleAlternative.AlternativeData _))
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
            if (originalData.actionData[0].IsModuleContainerFor(out ActionModuleAlternative.AlternativeData dataModule))
            {
                MultiActionManager.SetMappingForEntity(originalData.holdingEntity.entityId, dataModule.mapping);
            }
        }
    }
}

[HarmonyPatch]
public static class MultiItemPatches
{
    private static bool TryGetBoundData(ItemInventoryData originalData, out ItemInventoryData boundData)
    {
        if (originalData.IsModuleContainerFor(out MultiItemInvData multiInvData) && multiInvData.useBound && multiInvData.boundInvData != null)
        {
            boundData = multiInvData.boundInvData;
            return true;
        }
        boundData = null;
        return false;
    }

    // Match native Held selection without passing through the bound-data getter patches.
    public static ItemInventoryData GetOriginalHeldData(Hand hand)
    {
        if (hand == null)
            return null;
        var data = hand.bareHandData;
        if (hand.mode == Hand.HoldingMode.Transient && hand.transientData.HasItem)
            data = hand.transientData;
        else if (hand.mode == Hand.HoldingMode.Current && hand.toolbelt != null)
        {
            var slotData = hand.slotDatas[hand.toolbelt.SelectedSlot];
            if (slotData.item != null)
                data = slotData;
        }
        data.EnsureActionData();
        return data;
    }

    // Equip/rebuild identity stays physical even inside a bound action.
    private static IEnumerable<CodeInstruction> UseOriginalHeldData(IEnumerable<CodeInstruction> instructions)
    {
        var heldGetter = AccessTools.PropertyGetter(typeof(Hand), nameof(Hand.Held));
        var itemGetter = AccessTools.PropertyGetter(typeof(Hand), nameof(Hand.Item));
        var valueGetter = AccessTools.PropertyGetter(typeof(Hand), nameof(Hand.ItemValue));
        var originalGetter = AccessTools.Method(typeof(MultiItemPatches), nameof(GetOriginalHeldData));
        foreach (var code in instructions)
        {
            bool isItemGetter = code.Calls(itemGetter);
            bool isValueGetter = code.Calls(valueGetter);
            if (isItemGetter || isValueGetter || code.Calls(heldGetter))
            {
                code.opcode = OpCodes.Call;
                code.operand = originalGetter;
            }
            yield return code;
            if (isItemGetter || isValueGetter)
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ItemInventoryData), isItemGetter ? nameof(ItemInventoryData.item) : nameof(ItemInventoryData.itemValue)));
        }
    }

    [HarmonyPatch(typeof(Hand), nameof(Hand.Reconcile))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_Reconcile_Hand(IEnumerable<CodeInstruction> instructions)
    {
        return UseOriginalHeldData(instructions);
    }

    [HarmonyPatch(typeof(Hand), nameof(Hand.SelectHoldingMode))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_SelectHoldingMode_Hand(IEnumerable<CodeInstruction> instructions)
    {
        return UseOriginalHeldData(instructions);
    }

    [HarmonyPatch(typeof(Hand), nameof(Hand.SelectSlot))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_SelectSlot_Hand(IEnumerable<CodeInstruction> instructions)
    {
        return UseOriginalHeldData(instructions);
    }

    [HarmonyPatch(typeof(Hand), nameof(Hand.UsingBareHand))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_UsingBareHand_Hand(IEnumerable<CodeInstruction> instructions)
    {
        return UseOriginalHeldData(instructions);
    }

    [HarmonyPatch(typeof(Hand), nameof(Hand.TransitionRoutine), MethodType.Enumerator)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_TransitionRoutine_Hand(IEnumerable<CodeInstruction> instructions)
    {
        return UseOriginalHeldData(instructions);
    }

    [HarmonyPatch(typeof(Hand), nameof(Hand.Held), MethodType.Getter)]
    [HarmonyPostfix]
    public static void Postfix_Held_Hand(ref ItemInventoryData __result)
    {
        if (TryGetBoundData(__result, out var boundData))
            __result = boundData;
    }

    [HarmonyPatch(typeof(Hand), nameof(Hand.GetSlotData))]
    [HarmonyPostfix]
    public static void Postfix_GetSlotData_Hand(ref ItemInventoryData __result)
    {
        if (TryGetBoundData(__result, out var boundData))
            __result = boundData;
    }

    [HarmonyPatch(typeof(Inventory), "Item", MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix_Item_Inventory(Inventory __instance, int _idx, ref ItemStack __result)
    {
        if (TryGetBoundData(__instance.Hand.slotDatas[_idx], out var boundData))
        {
            __result = boundData.itemStack;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetItemStack))]
    [HarmonyPrefix]
    public static bool Prefix_GetItemStack_Inventory(Inventory __instance, int _idx, ref ItemStack __result)
    {
        return Prefix_Item_Inventory(__instance, _idx, ref __result);
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetItem))]
    [HarmonyPrefix]
    public static bool Prefix_GetItem_Inventory(Inventory __instance, int _idx, ref ItemStack __result)
    {
        return Prefix_Item_Inventory(__instance, _idx, ref __result);
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetItemInSlot))]
    [HarmonyPrefix]
    public static bool Prefix_GetItemInSlot_Inventory(Inventory __instance, int _idx, ref ItemClass __result)
    {
        if (TryGetBoundData(__instance.Hand.slotDatas[_idx], out var boundData))
        {
            __result = boundData.item;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SwapSelectedAmmo))]
    [HarmonyPrefix]
    private static bool Prefix_SwapSelectedAmmo_ItemActionRanged(EntityAlive _entity, int _ammoIndex)
    {
        if (GetOriginalHeldData(_entity.Hand).IsModuleContainerFor(out MultiItemInvData multiInvData) && multiInvData.IsBoundActionRunning())
        {
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.IsAimingGunPossible))]
    [HarmonyPostfix]
    private static void Postfix_IsAimingGunPossible_EntityPlayerLocal(EntityPlayerLocal __instance, ref bool __result)
    {
        if (__result && GetOriginalHeldData(__instance.Hand).IsModuleContainerFor(out MultiItemInvData multiInvData))
        {
            if (multiInvData.boundInvData != null)
            {
                bool useBound = multiInvData.useBound;
                var prevData = __instance.MinEventContext.ItemActionData;
                multiInvData.SetBoundParams();
                for (int i = 0; i < multiInvData.boundInvData.actionData.Count; i++)
                {
                    var action = multiInvData.boundItemClass.Actions[i];
                    var actionData = multiInvData.boundInvData.actionData[i];
                    if (action != null && !action.IsAimingGunPossible(actionData))
                    {
                        __result = false;
                        break;
                    }
                }
                multiInvData.RestoreParams(useBound);
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
            if (GetOriginalHeldData(holdingEntity.Hand).IsModuleContainerFor(out MultiItemInvData multiInvData))
            {
                if (multiInvData.boundInvData != null)
                {
                    __result = !multiInvData.IsBoundActionRunning();
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMoveController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var itemGetter = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItem));

        for (int i = 0; i < codes.Count; i++)
        {
            //ItemAction
            if (codes[i].Calls(itemGetter) && i + 1 < codes.Count && codes[i + 1].IsStloc())
            {
                codes.InsertRange(i + 2, new[]
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

        if (PlayerActionKFLib.Instance.Enabled && (!player.Hand.IsSwitching || wasReleased) && (wasPressed || wasReleased) && GetOriginalHeldData(player.Hand).IsModuleContainerFor(out MultiItemInvData multiInvData))
        {
            ItemModuleMultiItem.CheckAltMelee(player, multiInvData, wasReleased);
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