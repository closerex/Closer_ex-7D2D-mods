using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System;

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(MetadataAsHUDStatDataWithPassiveMax))]
public class ActionModuleMetadataAsHUDStatWithPassiveMax : MetadataAsHUDStatAbs
{
    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemAction __instance, ItemActionData _data, MetadataAsHUDStatDataWithPassiveMax __customData)
    {
        base.Postfix_OnModificationsChanged(__instance, _data, __customData);
        string str = __instance.Properties.GetString("PassiveMaxTags");
        str = _data.invData.itemValue.GetPropertyOverrideForAction("PassiveMaxTags", str, actionIndex);
        if (!string.IsNullOrEmpty(str))
        {
            __customData.tags = FastTags<TagGroup.Global>.Parse(str);
        }
        str = __instance.Properties.GetString("PassiveMaxEffect");
        str = _data.invData.itemValue.GetPropertyOverrideForAction("PassiveMaxEffect", str, actionIndex);
        __customData.passive = !string.IsNullOrEmpty(str) ? CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>(str, true) : PassiveEffects.None;
    }

    private float GetPassiveMaxValue(ItemInventoryData invData, ItemActionData data, MetadataAsHUDStatDataWithPassiveMax customData)
    {
        if (customData.tags.IsEmpty || customData.passive == PassiveEffects.None)
        {
            return 0f;
        }
        invData.holdingEntity.MinEventContext.ItemInventoryData = invData;
        invData.holdingEntity.MinEventContext.ItemValue = invData.itemValue;
        invData.holdingEntity.MinEventContext.ItemActionData = data;
        return EffectManager.GetValue(customData.passive, invData.itemValue, 0f, invData.holdingEntity, null, customData.tags);
    }

    public override bool UpdateActiveItemAmmo(ItemInventoryData invData, ref int currentAmmoCount)
    {
        var data = GetDataFromInvData<MetadataAsHUDStatDataWithPassiveMax>(invData);
        if (data != null)
        {
            currentAmmoCount = (int)GetPassiveMaxValue(invData, GetActionDataFromInvData(invData), data);
            return true;
        }
        return false;
    }

    public class MetadataAsHUDStatDataWithPassiveMax : MetadataAsHUDStatData
    {
        public FastTags<TagGroup.Global> tags;
        public PassiveEffects passive = PassiveEffects.None;
    }
}
