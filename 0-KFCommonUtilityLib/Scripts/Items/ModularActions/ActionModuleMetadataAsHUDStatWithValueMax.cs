using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System;

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(MetadataAsHUDStatWithValueMaxData))]
public class ActionModuleMetadataAsHUDStatWithValueMax : MetadataAsHUDStatAbs
{
    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemAction __instance, ItemActionData _data, MetadataAsHUDStatWithValueMaxData __customData)
    {
        base.Postfix_OnModificationsChanged(__instance, _data, __customData);
        __customData.statType = ValueRefStatType.Value;
        __customData.value = 0;
        __customData.refStr = "";
        string str = "";
        __instance.Properties.ParseString("MaxValue", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MaxValue", str, actionIndex);
        if (!string.IsNullOrEmpty(str))
        {
            if (float.TryParse(str, out float value))
            {
            __customData.statType = ValueRefStatType.Value;
                __customData.value = value;
            }
            else if (str.StartsWith('@'))
            {
                __customData.statType = ValueRefStatType.Cvar;
                __customData.refStr = str.Substring(1);
            }
            else if (str.StartsWith('#'))
            {
                __customData.statType = ValueRefStatType.Metadata;
                __customData.refStr = str.Substring(1);
            }
        }
    }

    private float GetMaxValue(ItemInventoryData invData, ItemActionData data, MetadataAsHUDStatWithValueMaxData customData)
    {
        switch (customData.statType)
        {
            case ValueRefStatType.Value:
                return customData.value;
            case ValueRefStatType.Cvar:
                return invData.holdingEntity.GetCVar(customData.refStr);
            case ValueRefStatType.Metadata:
                return Convert.ToSingle(invData.itemValue.GetMetadata(customData.refStr));
        }
        return 0;
    }

    public override bool UpdateActiveItemAmmo(ItemInventoryData invData, ref int currentAmmoCount)
    {
        var data = GetDataFromInvData<MetadataAsHUDStatWithValueMaxData>(invData);
        if (data != null)
        {
            currentAmmoCount = (int)GetMaxValue(invData, GetActionDataFromInvData(invData), data);
            return true;
        }
        return false;
    }

    public class MetadataAsHUDStatWithValueMaxData : MetadataAsHUDStatData
    {
        public ValueRefStatType statType;
        public string refStr;
        public float value;
    }
}