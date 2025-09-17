using GUI_2;
using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections;
using Unity.Mathematics;

[TypeTarget(typeof(ItemActionAttack)), TypeDataTarget(typeof(AlternativeData))]
public class ActionModuleAlternative
{
    internal static ItemValue InventorySetItemTemp;

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPrefix]
    private bool Prefix_StartHolding(ItemActionData _data, AlternativeData __customData)
    {
        //__customData.Init();
        int prevMode = __customData.mapping.CurMode;
        __customData.UpdateUnlockState(_data.invData.itemValue);
        if (prevMode != __customData.mapping.CurMode && _data.invData.holdingEntity is EntityPlayerLocal player)
        {
            MultiActionManager.FireToggleModeEvent(player, __customData.mapping);
        }
        MultiActionManager.SetMappingForEntity(_data.invData.holdingEntity.entityId, __customData.mapping);
        if (_data.invData.holdingEntity is EntityPlayerLocal)
        {
            MultiActionManager.inputCD = math.max(0.5f, MultiActionManager.inputCD);
            //ThreadManager.StartCoroutine(DelaySetExecutionIndex(_data.invData.holdingEntity, __customData.mapping));
        }
        return true;
    }

    //[MethodTargetPostfix(nameof(ItemActionAttack.StartHolding))]
    //private void Postfix_StartHolding(AlternativeData __customData)
    //{
    //    __customData.UpdateMuzzleTransformOverride();
    //    __customData.OverrideMuzzleTransform(__customData.mapping.CurMode);
    //}

    private static IEnumerator DelaySetExecutionIndex(EntityAlive player, MultiActionMapping mapping)
    {
        yield return null;
        yield return null;
        if (GameManager.Instance.GetGameStateManager().IsGameStarted())
            player?.emodel?.avatarController?.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, mapping.CurActionIndex);
    }

    [HarmonyPatch(nameof(ItemActionRanged.CancelReload)), MethodTargetPrefix]
    private bool Prefix_CancelReload(ItemActionData _actionData, AlternativeData __customData, bool holsterWeapon)
    {
        if (__customData.mapping == null)
            return true;
        int actionIndex = __customData.mapping.CurActionIndex;
        if (ConsoleCmdReloadLog.LogInfo)
            Log.Out($"cancel reload {actionIndex}");
        if (actionIndex == 0)
            return true;
        _actionData.invData.holdingEntity.inventory.holdingItem.Actions[actionIndex].CancelReload(_actionData.invData.holdingEntity.inventory.holdingItemData.actionData[actionIndex], holsterWeapon);
        return false;
    }

    [HarmonyPatch(nameof(ItemAction.CancelAction)), MethodTargetPrefix]
    private bool Prefix_CancelAction(ItemActionData _actionData, AlternativeData __customData)
    {
        if (__customData.mapping == null)
            return true;
        int actionIndex = __customData.mapping.CurActionIndex;
        if (ConsoleCmdReloadLog.LogInfo)
            Log.Out($"cancel action {actionIndex}");
        if (actionIndex == 0)
            return true;
        _actionData.invData.holdingEntity.inventory.holdingItem.Actions[actionIndex].CancelAction(_actionData.invData.holdingEntity.inventory.holdingItemData.actionData[actionIndex]);
        return false;
    }

    [HarmonyPatch(nameof(ItemAction.IsStatChanged)), MethodTargetPrefix]
    private bool Prefix_IsStatChanged(ref bool __result)
    {
        var mapping = MultiActionManager.GetMappingForEntity(GameManager.Instance.World.GetPrimaryPlayerId());
        __result |= mapping != null && mapping.CheckDisplayMode();
        return false;
    }

    //[MethodTargetPostfix(nameof(ItemActionAttack.StopHolding))]
    //private void Postfix_StopHolding(AlternativeData __customData)
    //{
    //    //moved to harmony patch
    //    //MultiActionManager.SetMappingForEntity(_data.invData.holdingEntity.entityId, null);
    //    __customData.mapping.SaveMeta();
    //}

    //todo: change to action specific property
    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationChanged(ItemActionData _data, ItemActionAttack __instance, AlternativeData __customData)
    {
        __instance.Properties.ParseString("ToggleActionSound", ref __customData.toggleSound);
        __customData.toggleSound = _data.invData.itemValue.GetPropertyOverrideForAction("ToggleActionSound", __customData.toggleSound, __instance.ActionIndex);
        __customData.mapping.toggleSound = __customData.toggleSound;
    }

    [HarmonyPatch(nameof(ItemAction.SetupRadial)), MethodTargetPrefix]
    private bool Prefix_SetupRadial(XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
    {
        var mapping = MultiActionManager.GetMappingForEntity(_epl.entityId);
        if (mapping != null)
        {
            var radialContextItem = new AlternativeRadialContextItem(mapping, _xuiRadialWindow, _epl);
            _xuiRadialWindow.SetCommonData(UIUtils.GetButtonIconForAction(_epl.playerInput.Reload), handleRadialCommand, radialContextItem, radialContextItem.PreSelectedIndex, false, radialValidTest);
        }

        return false;
    }

    private bool radialValidTest(XUiC_Radial _sender, XUiC_Radial.RadialContextAbs _context)
    {
        AlternativeRadialContextItem radialContextItem = _context as AlternativeRadialContextItem;
        if (radialContextItem == null)
        {
            return false;
        }
        EntityPlayerLocal entityPlayer = _sender.xui.playerUI.entityPlayer;
        return radialContextItem.mapping == MultiActionManager.GetMappingForEntity(entityPlayer.entityId) && radialContextItem.mapping.CurActionIndex == radialContextItem.ActionIndex;
    }

    //redirect reload call to shared meta action, which then sets ItemActionIndex animator param to its action index
    //for example if action 3 share meta with action 0, then ItemActionIndex is set to 0 on reload begin.
    //since event param item action data is set to the shared meta action data, all reload related passive calculation and trigger events goes there.
    private void handleRadialCommand(XUiC_Radial _sender, int _commandIndex, XUiC_Radial.RadialContextAbs _context)
    {
        AlternativeRadialContextItem radialContextItem = _context as AlternativeRadialContextItem;
        if (radialContextItem == null)
        {
            return;
        }
        EntityPlayerLocal entityPlayer = _sender.xui.playerUI.entityPlayer;
        if (radialContextItem.mapping == MultiActionManager.GetMappingForEntity(entityPlayer.entityId) && radialContextItem.mapping.CurActionIndex == radialContextItem.ActionIndex)
        {
            entityPlayer.MinEventContext.ItemActionData = entityPlayer.inventory.holdingItemData.actionData?[radialContextItem.ActionIndex];
            (entityPlayer.inventory.holdingItem.Actions?[radialContextItem.ActionIndex] as ItemActionRanged)?.SwapSelectedAmmo(entityPlayer, _commandIndex);
        }
    }

    public class AlternativeData
    {
        public MultiActionMapping mapping;
        public string toggleSound;
        public ItemInventoryData invData;
        //private bool inited = false;
        private readonly bool[] unlocked = new bool[MultiActionIndice.MAX_ACTION_COUNT];
        //public Transform[] altMuzzleTrans = new Transform[MultiActionIndice.MAX_ACTION_COUNT];
        //public Transform[] altMuzzleTransDBarrel = new Transform[MultiActionIndice.MAX_ACTION_COUNT];

        public AlternativeData(ItemInventoryData _inventoryData)
        {
            this.invData = _inventoryData;
            Init();
        }

        //public void UpdateMuzzleTransformOverride()
        //{
        //    for (int i = 0; i < MultiActionIndice.MAX_ACTION_COUNT; i++)
        //    {
        //        int curActionIndex = mapping.indices.GetActionIndexForMode(i);
        //        if (curActionIndex < 0)
        //        {
        //            break;
        //        }
        //        var rangedData = invData.actionData[curActionIndex] as ItemActionRanged.ItemActionDataRanged;
        //        if (rangedData != null)
        //        {
        //            if (rangedData.IsDoubleBarrel)
        //            {
        //                altMuzzleTrans[i] = AnimationRiggingManager.GetTransformOverrideByName($"Muzzle_L{curActionIndex}", rangedData.invData.model) ?? rangedData.muzzle;
        //                altMuzzleTransDBarrel[i] = AnimationRiggingManager.GetTransformOverrideByName($"Muzzle_R{curActionIndex}", rangedData.invData.model) ?? rangedData.muzzle2;
        //            }
        //            else
        //            {
        //                altMuzzleTrans[i] = AnimationRiggingManager.GetTransformOverrideByName($"Muzzle{curActionIndex}", rangedData.invData.model) ?? rangedData.muzzle;
        //            }
        //        }
        //    }
        //}

        public void Init()
        {
            //if (inited)
            //    return;

            //inited = true;
            MultiActionIndice indices = MultiActionManager.GetActionIndiceForItemID(invData.item.Id);
            mapping = new MultiActionMapping(this, indices, invData.holdingEntity, InventorySetItemTemp, toggleSound, invData.slotIdx, unlocked);
            UpdateUnlockState(InventorySetItemTemp);
        }

        public void UpdateUnlockState(ItemValue itemValue)
        {
            //if (!inited)
            //    return;
            unlocked[0] = true;
            for (int i = 1; i < mapping.ModeCount; i++)
            {
                bool flag = true;
                int actionIndex = mapping.indices.GetActionIndexForMode(i);
                ItemAction action = itemValue.ItemClass.Actions[actionIndex];
                action.Properties.ParseBool("ActionUnlocked", ref flag);
                if (bool.TryParse(itemValue.GetPropertyOverride($"ActionUnlocked_{actionIndex}", flag.ToString()), out bool overrideFlag))
                    flag = overrideFlag;
                unlocked[i] = flag;
            }
            //by the time we check unlock state, ItemValue in inventory slot might not be ready yet
            mapping.SaveMeta(itemValue);
            mapping.CurMode = mapping.CurMode;
            mapping.ReadMeta(itemValue);
        }

        public bool IsActionUnlocked(int actionIndex)
        {
            int mode = mapping.indices.GetModeForAction(actionIndex);
            if (mode >= MultiActionIndice.MAX_ACTION_COUNT || mode < 0)
                return false;
            return unlocked[mode];
        }

//        public void OverrideMuzzleTransform(int mode)
//        {
//            var rangedData = invData.actionData[mapping.indices.GetActionIndexForMode(mode)] as ItemActionRanged.ItemActionDataRanged;
//            if (rangedData != null)
//            {
//                if (rangedData.IsDoubleBarrel)
//                {
//                    rangedData.muzzle = altMuzzleTrans[mode];
//                    rangedData.muzzle2 = altMuzzleTransDBarrel[mode];
//                }
//                else
//                {
//                    rangedData.muzzle = altMuzzleTrans[mode];
//                }
//            }
//#if DEBUG
//            Log.Out($"setting muzzle transform for action {rangedData.indexInEntityOfAction} to {rangedData.muzzle.name}\n{StackTraceUtility.ExtractStackTrace()}");
//#endif
//        }
    }

    //todo: don't setup for every mode, and use reload animation from shared action
    public class AlternativeRadialContextItem : XUiC_Radial.RadialContextAbs
    {
        public MultiActionMapping mapping;

        public int ActionIndex { get; private set; }
        public int PreSelectedIndex { get; private set; }

        public AlternativeRadialContextItem(MultiActionMapping mapping, XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
        {
            this.mapping = mapping;
            ActionIndex = mapping.CurActionIndex;
            PreSelectedIndex = mapping.SetupRadial(_xuiRadialWindow, _epl);
        }
    }
}
