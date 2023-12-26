using GUI_2;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Singletons;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TypeTarget(typeof(ItemActionRanged), typeof(AlternativeData))]
public class ActionModuleAlternative
{
    [MethodTargetPrefix(nameof(ItemActionRanged.StartHolding))]
    private bool Prefix_StartHolding(ItemActionData _data, AlternativeData __customData)
    {
        MultiActionManager.SetMappingForEntity(_data.invData.holdingEntity.entityId, __customData.mapping);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StopHolding))]
    private void Postfix_StopHolding(ItemActionData _data)
    {
        MultiActionManager.SetMappingForEntity(_data.invData.holdingEntity.entityId, null);
    }

    //todo: change to action specific property
    [MethodTargetPostfix(nameof(ItemActionRanged.OnModificationsChanged))]
    private void Postfix_OnModificationChanged(ItemActionData _data, ItemActionRanged __instance, AlternativeData __customData)
    {
        __instance.Properties.ParseString("ToggleActionSound", ref __customData.toggleSound);
        __customData.toggleSound = _data.invData.itemValue.GetPropertyOverride("ToggleActionSound", __customData.toggleSound);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.SetupRadial))]
    private bool Prefix_SetupRadial(XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl, AlternativeData __customData)
    {
        var radialContextItem = new AlternativeRadialContextItem(__customData.mapping, _xuiRadialWindow, _epl);
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
        return radialContextItem.mapping == MultiActionManager.GetMappingForEntity(entityPlayer.entityId) && radialContextItem.mapping.CurMetaIndex == radialContextItem.MetaActionIndex;
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
        if (radialContextItem.mapping == MultiActionManager.GetMappingForEntity(entityPlayer.entityId) && radialContextItem.mapping.CurMetaIndex == radialContextItem.MetaActionIndex)
        {
            entityPlayer.MinEventContext.ItemActionData = entityPlayer.inventory.holdingItemData.actionData[radialContextItem.MetaActionIndex];
            ((ItemActionRanged)entityPlayer.inventory.holdingItem.Actions[radialContextItem.MetaActionIndex]).SwapSelectedAmmo(entityPlayer, _commandIndex);
        }
    }

    public class AlternativeData
    {
        public MultiActionMapping mapping;
        public string toggleSound;

        public AlternativeData(ItemInventoryData invData, int actionIndex, ActionModuleAlternative module)
        {
            MultiActionIndice indices = new MultiActionIndice(invData.item.Actions);
            ItemValue itemValue = invData.itemValue;
            mapping = new MultiActionMapping(indices, itemValue, toggleSound);
        }
    }

    //todo: don't setup for every mode, and use reload animation from shared action
    public class AlternativeRadialContextItem : XUiC_Radial.RadialContextAbs
    {
        public MultiActionMapping mapping;

        public int MetaActionIndex { get; private set; }
        public int PreSelectedIndex { get; private set; }

        public AlternativeRadialContextItem(MultiActionMapping mapping, XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
        {
            this.mapping = mapping;
            MetaActionIndex = mapping.CurMetaIndex;
            PreSelectedIndex = mapping.SetupRadial(_xuiRadialWindow, _epl);;
        }
    }
}
