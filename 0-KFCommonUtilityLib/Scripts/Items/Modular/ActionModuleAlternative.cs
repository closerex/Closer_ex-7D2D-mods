using GUI_2;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections;
using Unity.Mathematics;

[TypeTarget(typeof(ItemActionAttack), typeof(AlternativeData))]
public class ActionModuleAlternative
{
    [MethodTargetPrefix(nameof(ItemActionAttack.StartHolding))]
    private bool Prefix_StartHolding(ItemActionData _data, AlternativeData __customData)
    {
        __customData.Init();
        MultiActionManager.SetMappingForEntity(_data.invData.holdingEntity.entityId, __customData.mapping);
        if (_data.invData.holdingEntity is EntityPlayerLocal)
        {
            MultiActionManager.inputCD = math.max(0.5f, MultiActionManager.inputCD);
            ThreadManager.StartCoroutine(DelaySetExecutionIndex(_data.invData.holdingEntity, __customData.mapping));
        }
        return true;
    }

    private static IEnumerator DelaySetExecutionIndex(EntityAlive player, MultiActionMapping mapping)
    {
        yield return null;
        yield return null;
        player?.emodel?.avatarController?.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, mapping.CurActionIndex);
    }

    //[MethodTargetPrefix(nameof(ItemActionRanged.CancelReload))]
    //private bool Prefix_CancelReload(ItemActionData _actionData, AlternativeData __customData)
    //{
    //    int actionIndex = __customData.mapping.CurActionIndex;
    //    Log.Out($"cancel reload {actionIndex}");
    //    if(actionIndex == 0)
    //        return true;
    //    _actionData.invData.holdingEntity.inventory.holdingItem.Actions[actionIndex].CancelReload(_actionData.invData.holdingEntity.inventory.holdingItemData.actionData[actionIndex]);
    //    return false;
    //}

    [MethodTargetPrefix(nameof(ItemActionAttack.CancelAction))]
    private bool Prefix_CancelAction(ItemActionData _actionData, AlternativeData __customData)
    {
        int actionIndex = __customData.mapping.CurActionIndex;
        Log.Out($"cancel action {actionIndex}");
        if(actionIndex == 0)
            return true;
        _actionData.invData.holdingEntity.inventory.holdingItem.Actions[actionIndex].CancelAction(_actionData.invData.holdingEntity.inventory.holdingItemData.actionData[actionIndex]);
        return false;
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.IsStatChanged))]
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
    [MethodTargetPostfix(nameof(ItemActionAttack.OnModificationsChanged))]
    private void Postfix_OnModificationChanged(ItemActionData _data, ItemActionAttack __instance, AlternativeData __customData)
    {
        __instance.Properties.ParseString("ToggleActionSound", ref __customData.toggleSound);
        __customData.toggleSound = _data.invData.itemValue.GetPropertyOverride("ToggleActionSound", __customData.toggleSound);
        __customData.mapping.toggleSound = __customData.toggleSound;
    }

    [MethodTargetPrefix(nameof(ItemActionAttack.SetupRadial))]
    private bool Prefix_SetupRadial(XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
    {
        var radialContextItem = new AlternativeRadialContextItem(MultiActionManager.GetMappingForEntity(_epl.entityId), _xuiRadialWindow, _epl);
        _xuiRadialWindow.SetCommonData(UIUtils.ButtonIcon.FaceButtonEast, new XUiC_Radial.CommandHandlerDelegate(this.handleRadialCommand), radialContextItem, radialContextItem.PreSelectedIndex, false, new XUiC_Radial.RadialStillValidDelegate(this.radialValidTest));
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
            entityPlayer.MinEventContext.ItemActionData = entityPlayer.inventory.holdingItemData.actionData[radialContextItem.ActionIndex];
            ((ItemActionRanged)entityPlayer.inventory.holdingItem.Actions[radialContextItem.ActionIndex]).SwapSelectedAmmo(entityPlayer, _commandIndex);
        }
    }

    public class AlternativeData
    {
        public MultiActionMapping mapping;
        public string toggleSound;
        public ItemInventoryData invData;
        private bool inited = false;

        public AlternativeData(ItemInventoryData invData, int actionIndex, ActionModuleAlternative module)
        {
            this.invData = invData;
        }

        public void Init()
        {
            if(inited)
                return;

            inited = true;
            MultiActionIndice indices = MultiActionManager.GetActionIndiceForItemID(invData.item.Id);
            mapping = new MultiActionMapping(indices, invData.holdingEntity, toggleSound, invData.slotIdx);
        }
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
            PreSelectedIndex = mapping.SetupRadial(_xuiRadialWindow, _epl);;
        }
    }
}
