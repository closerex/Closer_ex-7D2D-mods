using GUI_2;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionThrowAway)), TypeDataTarget(typeof(ThrowFromInventoryData))]
public class ActionModuleThrowFromInventory : IDisplayAsHUDStat
{
    public string[] throwItems;
    public ItemValue[] throwItemValues;
    public ItemAction action;
    private bool itemValidated;
    public float swapAmmoDelay;

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        action = __instance;
        if (_props.Contains("ThrowItems"))
        {
            throwItems = _props.GetString("ThrowItems").Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < throwItems.Length; i++)
            {
                throwItems[i] = throwItems[i].Trim();
            }
        }
        if (throwItems == null || throwItems.Length == 0)
        {
            throw new Exception($"No throw item specified for item {__instance.item.Name} action index {__instance.ActionIndex}");
        }
        swapAmmoDelay = 0.1f;
        _props.ParseFloat("SwapAmmoDelay", ref swapAmmoDelay);
    }

    [HarmonyPatch(typeof(ItemActionThrowAway), nameof(ItemAction.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(ItemActionData _data, ItemActionThrowAway __instance, ThrowFromInventoryData __customData)
    {
        if (!itemValidated)
        {
            ValidateItems();
        }

        EntityAlive holdingEntity = __customData.invData.holdingEntity;
        EntityPlayerLocal player = holdingEntity as EntityPlayerLocal;
        if (player != null)
        {
            player.InventoryChangedEvent -= __customData.OnInventoryUpdate;
            player.InventoryChangedEvent += __customData.OnInventoryUpdate;
        }

        var itemValue = __customData.invData.itemValue;
        if (itemValue.SelectedAmmoTypeIndex < 0 || itemValue.SelectedAmmoTypeIndex >= throwItemValues.Length)
        {
            __customData.SetAmmoIndex(0);
        }
        else
        {
            __customData.CurrentAmmoValue.FireEvent(CustomEnums.onThrowItemSelected, holdingEntity.MinEventContext);
            __customData.OnInventoryUpdate();
            __customData.SyncThrowParams();
        }
        __customData.swapAmmoStartTime = -1;
        __customData.invData.holdingEntity.emodel.avatarController?.CancelEvent("ThrowItemChanged");
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(ItemActionData _data, ThrowFromInventoryData __customData)
    {
        EntityPlayerLocal player = _data.invData.holdingEntity as EntityPlayerLocal;
        if (player != null)
        {
            player.InventoryChangedEvent -= __customData.OnInventoryUpdate;
        }
        __customData.CurrentAmmoValue.FireEvent(CustomEnums.onThrowItemSwapped, _data.invData.holdingEntity.MinEventContext);
        __customData.swapAmmoStartTime = -1;
        __customData.invData.holdingEntity.emodel.avatarController?.CancelEvent("ThrowItemChanged");
    }

    [HarmonyPatch(nameof(ItemAction.CanExecute)), MethodTargetPrefix]
    public bool Prefix_CanExecute(ref bool __result, ThrowFromInventoryData __customData)
    {
        __result = __customData.ammoCount > 0;
        return false;
    }

    [HarmonyPatch(nameof(ItemAction.HasRadial)), MethodTargetPrefix]
    public bool Prefix_HasRadial(ref bool __result)
    {
        __result = itemValidated && throwItemValues != null && throwItemValues.Length > 0;
        return false;
    }

    [HarmonyPatch(typeof(ItemActionThrowAway), nameof(ItemActionThrowAway.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionThrowAway_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var prop_attack = AccessTools.PropertySetter(typeof(EntityAlive), nameof(EntityAlive.RightArmAnimationAttack));

        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].Calls(prop_attack))
            {
                codes.InsertRange(i + 1, new[]
                {
                    CodeInstruction.LoadArgument(1),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ThrowFromInventoryData>)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<ThrowFromInventoryData>), nameof(IModuleContainerFor<ThrowFromInventoryData>.Instance))),
                    CodeInstruction.Call(typeof(ThrowFromInventoryData), nameof(ThrowFromInventoryData.OnThrowAnimationBegin))
                });
                break;
            }
        }
        return codes;
    }

    [HarmonyPatch(nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    public void Postfix_IsActionRunning(ref bool __result, ThrowFromInventoryData __customData)
    {
        __result |= __customData.IsSwappingAmmoProgressing;
    }

    [HarmonyPatch(nameof(ItemAction.ItemActionEffects)), MethodTargetPrefix]
    public bool Prefix_ItemActionEffects(ThrowFromInventoryData __customData, int _firingState, int _userData)
    {
        if (__customData.invData.holdingEntity.isEntityRemote)
        {
            __customData.SetAmmoIndex((byte)_firingState);
        }
        else
        {
            __customData.swapAmmoStartTime = Time.time;
            __customData.targetAmmoType = (byte)_firingState;
            __customData.invData.holdingEntity.emodel.avatarController?.TriggerEvent("ThrowItemChanged");
        }
        return false;
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    public void Postfix_OnHoldingUpdate(ThrowFromInventoryData __customData)
    {
        if (__customData.invData.holdingEntity.isEntityRemote || !__customData.IsSwappingAmmoFinished)
        {
            return;
        }
        if (__customData.targetAmmoType < 0 || __customData.targetAmmoType >= throwItemValues.Length)
        {
            __customData.SetAmmoIndex(0);
        }
        else
        {
            __customData.SetAmmoIndex(__customData.targetAmmoType);
        }
        __customData.invData.holdingEntity.emodel.avatarController?.CancelEvent("ThrowItemChanged");
    }

    [HarmonyPatch(nameof(ItemActionThrowAway.throwAway)), MethodTargetPrefix]
    public bool Prefix_throwAway(ItemActionThrowAway.MyInventoryData _actionData)
    {
        if (_actionData.invData.holdingEntity.GetItemCount(throwItemValues[_actionData.invData.itemValue.SelectedAmmoTypeIndex]) == 0)
        {
            _actionData.m_bActivated = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionThrowAway), nameof(ItemActionThrowAway.ExecuteAction))]
    [HarmonyPatch(typeof(ItemActionThrowAway), nameof(ItemActionThrowAway.OnScreenOverlay))]
    [MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_FieldReplacer(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_defstr = AccessTools.Field(typeof(ItemActionThrowAway), nameof(ItemActionThrowAway.defaultThrowStrength));
        var fld_maxstr = AccessTools.Field(typeof(ItemActionThrowAway), nameof(ItemActionThrowAway.maxThrowStrength));
        var fld_maxtime = AccessTools.Field(typeof(ItemActionThrowAway), nameof(ItemActionThrowAway.maxStrainTime));
        var fld_override_defstr = AccessTools.Field(typeof(ThrowFromInventoryData), nameof(ThrowFromInventoryData.defaultThrowStrength));
        var fld_override_maxstr = AccessTools.Field(typeof(ThrowFromInventoryData), nameof(ThrowFromInventoryData.maxThrowStrength));
        var fld_override_maxtime = AccessTools.Field(typeof(ThrowFromInventoryData), nameof(ThrowFromInventoryData.maxStrainTime));

        var lbd_data = generator.DeclareLocal(typeof(ThrowFromInventoryData));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_defstr))
            {
                codes[i - 1].opcode = OpCodes.Ldloc_S;
                codes[i - 1].operand = lbd_data;
                codes[i].operand = fld_override_defstr;
            }
            else if (codes[i].LoadsField(fld_maxstr))
            {
                codes[i - 1].opcode = OpCodes.Ldloc_S;
                codes[i - 1].operand = lbd_data;
                codes[i].operand = fld_override_maxstr;
            }
            else if (codes[i].LoadsField(fld_maxtime))
            {
                codes[i - 1].opcode = OpCodes.Ldloc_S;
                codes[i - 1].operand = lbd_data;
                codes[i].operand = fld_override_maxtime;
            }
        }

        codes.InsertRange(0, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ThrowFromInventoryData>)),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<ThrowFromInventoryData>), nameof(IModuleContainerFor<ThrowFromInventoryData>.Instance))),
            CodeInstruction.StoreLocal(lbd_data.LocalIndex)
        });

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionThrowAway), nameof(ItemActionThrowAway.throwAway)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionThrowAway_throwAway(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var lbd_iv = generator.DeclareLocal(typeof(ItemValue));

        var prop_iv = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemItemValue));
        var prop_instance = AccessTools.PropertyGetter(typeof(IModuleContainerFor<ThrowFromInventoryData>), nameof(IModuleContainerFor<ThrowFromInventoryData>.Instance));
        var prop_ammovalue = AccessTools.PropertyGetter(typeof(ThrowFromInventoryData), nameof(ThrowFromInventoryData.CurrentAmmoValue));
        var mtd_dec = AccessTools.Method(typeof(Inventory), nameof(Inventory.DecHoldingItem));

        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].Calls(prop_iv) && codes[i + 1].opcode == OpCodes.Ldc_I4_1)
            {
                codes.RemoveRange(i - 2, 3);
                codes.InsertRange(i - 2, new[]
                {
                    CodeInstruction.LoadArgument(1),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ThrowFromInventoryData>)),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance),
                    new CodeInstruction(OpCodes.Call, prop_ammovalue),
                    new CodeInstruction(OpCodes.Dup),
                    CodeInstruction.StoreLocal(lbd_iv.LocalIndex)
                });
                i += 3;
            }
            else if (codes[i].Calls(mtd_dec))
            {
                var lbl = codes[i + 1].operand;
                codes.RemoveRange(i - 2, 4);
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    CodeInstruction.LoadLocal(lbd_iv.LocalIndex),
                    CodeInstruction.Call(typeof(EntityInventoryExtension), nameof(EntityInventoryExtension.TryRemoveItem)),
                    new CodeInstruction(OpCodes.Pop),
                    CodeInstruction.LoadArgument(1),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ThrowFromInventoryData>)),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance),
                    CodeInstruction.LoadField(typeof(ThrowFromInventoryData), nameof(ThrowFromInventoryData.ammoCount)),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Bgt_Un_S, lbl)
                });
                i += 6;
            }
        }

        return codes;
    }

    [HarmonyPatch(nameof(ItemAction.SetupRadial)), MethodTargetPrefix]
    public bool Prefix_SetupRadial(XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
    {
        if (_epl.inventory.holdingItem.IsActionRunning(_epl.inventory.holdingItemData))
        {
            return false;
        }
        _xuiRadialWindow.ResetRadialEntries();
        int defaultIndex = -1;
        ThrowFromInventoryData customData = ((IModuleContainerFor<ThrowFromInventoryData>)_epl.inventory.holdingItemData.actionData[action.ActionIndex]).Instance;
        for (int i = 0; i < throwItemValues.Length; i++)
        {
            ItemValue itemValue = throwItemValues[i];
            ItemClass itemClass = itemValue.ItemClass;
            int itemCount = _epl.GetItemCount(itemValue);
            bool isCurrent = i == _epl.inventory.holdingItemItemValue.SelectedAmmoTypeIndex;
            _xuiRadialWindow.CreateRadialEntry(i, itemClass.GetIconName(), (itemCount > 0) ? "ItemIconAtlas" : "ItemIconAtlasGreyscale", itemCount.ToString(), itemClass.GetLocalizedItemName(), isCurrent);
            if (isCurrent)
            {
                defaultIndex = i;
            }
        }
        _xuiRadialWindow.SetCommonData(UIUtils.GetButtonIconForAction(_epl.playerInput.Reload), handleRadialCommand, new RadialContextItem(customData), defaultIndex, false, radialValidTest);
        return false;
    }

    public void ValidateItems()
    {
        itemValidated = true;
        List<ItemValue> list_ids = new(throwItems.Length);
        foreach (var item in throwItems)
        {
            var itemClass = ItemClass.GetItemClass(item);
            if (itemClass != null)
            {
                list_ids.Add(ItemClass.GetItem(item));
            }
        }
        throwItemValues = list_ids.ToArray();
        if (throwItemValues.Length == 0)
        {
            throw new Exception($"No valid throw item!");
        }
        throwItems = Array.ConvertAll(throwItemValues, static (ItemValue itemValue) => itemValue.ItemClass.GetItemName());
    }

    public static void handleRadialCommand(XUiC_Radial _sender, int _commandIndex, XUiC_Radial.RadialContextAbs _context)
    {
        if (_context is not RadialContextItem customContext || customContext.data == null)
        {
            return;
        }
        if (customContext.data.invData.itemValue.SelectedAmmoTypeIndex == _commandIndex && customContext.data.invData.actionData[0] is IModuleContainerFor<ActionModuleInspectable.InspectableData> inspectData)
        {
            if (inspectData.Instance.CanInspect(customContext.data.ammoCount))
            {
                inspectData.Instance.TriggerInspect();
            }
            return;
        }
        EntityPlayerLocal player = _sender.xui.playerUI.entityPlayer;
        GameManager.Instance.ItemActionEffectsServer(player.entityId, customContext.data.invData.slotIdx, customContext.data.module.action.ActionIndex, _commandIndex, Vector3.zero, Vector3.one);
    }

    public static bool radialValidTest(XUiC_Radial _sender, XUiC_Radial.RadialContextAbs _context)
    {
        if (_context is not RadialContextItem customContext || customContext.data == null)
        {
            return false;
        }
        EntityPlayerLocal player = _sender.xui.playerUI.entityPlayer;
        return player.inventory.holdingItemData == customContext.data.invData;
    }

    public string GetHUDStatValue(ItemInventoryData invData)
    {
        if ((invData.actionData[action.ActionIndex] as IModuleContainerFor<ThrowFromInventoryData>)?.Instance is ThrowFromInventoryData data)
        {
            return data.ammoCount.ToString();
        }
        return "";
    }

    public string GetHUDStatValueWithMax(ItemInventoryData invData, int currentAmmoCount)
    {
        return currentAmmoCount.ToString();
    }

    public float GetHUDStatFillFraction(ItemInventoryData invData, int currentAmmoCount)
    {
        return Mathf.Clamp01(currentAmmoCount);
    }

    public bool UpdateActiveItemAmmo(ItemInventoryData invData, ref int currentAmmoCount)
    {
        if ((invData.actionData[action.ActionIndex] as IModuleContainerFor<ThrowFromInventoryData>)?.Instance is ThrowFromInventoryData data)
        {
            currentAmmoCount = data.ammoCount;
            return true;
        }
        return false;
    }

    public void GetIconOverride(ItemInventoryData invData, ref string originalIcon)
    {
        if ((invData.actionData[action.ActionIndex] as IModuleContainerFor<ThrowFromInventoryData>)?.Instance is ThrowFromInventoryData data)
        {
            originalIcon = data.CurrentAmmoValue.ItemClass.GetIconName();
        }
    }

    public void GetIconTintOverride(ItemInventoryData invData, ref Color32 originalTint)
    {
        if ((invData.actionData[action.ActionIndex] as IModuleContainerFor<ThrowFromInventoryData>)?.Instance is ThrowFromInventoryData data)
        {
            originalTint = data.CurrentAmmoValue.ItemClass.GetIconTint();
        }
    }

    public class ThrowFromInventoryData
    {
        public int ammoCount;
        public ItemInventoryData invData;
        public ActionModuleThrowFromInventory module;
        public float swapAmmoStartTime;
        public byte targetAmmoType;

        public float defaultThrowStrength;
        public float maxThrowStrength;
        public float maxStrainTime;

        public ItemValue CurrentAmmoValue => module.throwItemValues[invData.itemValue.SelectedAmmoTypeIndex];
        public bool IsSwappingAmmoProgressing => swapAmmoStartTime >= 0 && Time.time - swapAmmoStartTime < module.swapAmmoDelay;
        public bool IsSwappingAmmoFinished => swapAmmoStartTime >= 0 && Time.time - swapAmmoStartTime >= module.swapAmmoDelay;

        public ThrowFromInventoryData(ItemInventoryData _inventoryData, ActionModuleThrowFromInventory __customModule)
        {
            invData = _inventoryData;
            module = __customModule;
        }

        public void OnInventoryUpdate()
        {
            ammoCount = invData.holdingEntity.GetItemCount(module.throwItemValues[invData.itemValue.SelectedAmmoTypeIndex]);
            invData.holdingEntity.emodel.avatarController?.UpdateInt(AnimationAmmoUpdateState.hash_states[0], ammoCount);
        }

        public void OnThrowAnimationBegin()
        {
            invData.holdingEntity.emodel.avatarController?.UpdateInt(AnimationAmmoUpdateState.hash_states[0], ammoCount - 1);
        }

        public void SetAmmoIndex(byte index)
        {
            if (index != invData.itemValue.SelectedAmmoTypeIndex)
            {
                invData.holdingEntity.MinEventContext.Transform = invData.model;
                CurrentAmmoValue.FireEvent(CustomEnums.onThrowItemSwapped, invData.holdingEntity.MinEventContext);
                invData.itemValue.SelectedAmmoTypeIndex = index;
                CurrentAmmoValue.FireEvent(CustomEnums.onThrowItemSelected, invData.holdingEntity.MinEventContext);
                invData.Changed();
            }
            swapAmmoStartTime = -1;
            targetAmmoType = index;
            SyncThrowParams();
        }

        public void SyncThrowParams()
        {
            var throwAction = CurrentAmmoValue.ItemClass.Actions.First(a => a is ItemActionThrowAway) as ItemActionThrowAway;
            defaultThrowStrength = throwAction.defaultThrowStrength;
            maxThrowStrength = throwAction.maxThrowStrength;
            maxStrainTime = throwAction.maxStrainTime;
        }
    }

    public class RadialContextItem : XUiC_Radial.RadialContextAbs
    {
        public ThrowFromInventoryData data;

        public RadialContextItem(ThrowFromInventoryData data)
        {
            this.data = data;
        }
    }
}
