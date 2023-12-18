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
    private static bool Prefix_StartHolding(ItemActionData _data, AlternativeData __customData)
    {
        MultiActionManager.SetMappingForEntity(_data.invData.holdingEntity.entityId, __customData.mapping);
        return true;
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StopHolding))]
    private static void Postfix_StopHolding(ItemActionData _data)
    {
        MultiActionManager.SetMappingForEntity(_data.invData.holdingEntity.entityId, null);
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.OnModificationsChanged))]
    private static void Postfix_OnModificationChanged(ItemActionData _data, ItemActionRanged __instance, AlternativeData __customData)
    {
        __instance.Properties.ParseString("ToggleActionSound", ref __customData.toggleSound);
        __customData.toggleSound = _data.invData.itemValue.GetPropertyOverride("ToggleActionSound", __customData.toggleSound);
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
}
