using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using UnityEngine;

public abstract class MetadataAsHUDStatAbs : IDisplayAsHUDStat
{
    public int actionIndex = 0;

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(ItemAction __instance, DynamicProperties _props)
    {
        actionIndex = __instance.ActionIndex;
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemAction __instance, ItemActionData _data, MetadataAsHUDStatData __customData)
    {
        __customData.metaName = "";
        __instance.Properties.ParseString("DisplayMetadata", ref __customData.metaName);
        __customData.metaName = _data.invData.itemValue.GetPropertyOverrideForAction("DisplayMetadata", __customData.metaName, actionIndex);
        
        __customData.format = "{0:0}";
        __instance.Properties.ParseString("DisplayFormat", ref __customData.format);
        __customData.format = _data.invData.itemValue.GetPropertyOverrideForAction("DisplayFormat", __customData.format, actionIndex);

        __customData.formatWithMax = "{0:0}/{1:0}";
        __instance.Properties.ParseString("DisplayFormatWithMax", ref __customData.formatWithMax);
        __customData.formatWithMax = _data.invData.itemValue.GetPropertyOverrideForAction("DisplayFormatWithMax", __customData.formatWithMax, actionIndex);

        __customData.isPerc = false;
        __instance.Properties.ParseBool("MaxAsPercentage", ref __customData.isPerc);
        __customData.isPerc = bool.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("MaxAsPercentage", __customData.isPerc.ToString(), actionIndex));

        __customData.iconOverride = "";
        __instance.Properties.ParseString("IconOverride", ref __customData.iconOverride);
        __customData.iconOverride = _data.invData.itemValue.GetPropertyOverrideForAction("IconOverride", __customData.iconOverride, actionIndex);
        
        __customData.iconTintOverride = null;
        string colorStr = "";
        __instance.Properties.ParseString("IconTintOverride", ref colorStr);
        colorStr = _data.invData.itemValue.GetPropertyOverrideForAction("IconTintOverride", colorStr, actionIndex);
        if (!string.IsNullOrEmpty(colorStr))
        {
            __customData.iconTintOverride = StringParsers.ParseHexColor(colorStr);
        }
    }

    public virtual float GetHUDStatFillFraction(ItemInventoryData invData, int currentAmmoCount)
    {
        if (currentAmmoCount > 0 && GetHUDStatValueAsFloat(invData, out float value))
            return value / currentAmmoCount;
        return 0f;
    }

    public virtual string GetHUDStatValue(ItemInventoryData invData)
    {
        var data = GetDataFromInvData<MetadataAsHUDStatData>(invData);
        if (data != null && !string.IsNullOrEmpty(data.format) && GetHUDStatValueAsFloat(invData, out float value))
        {
            return string.Format(data.format, value);
        }

        return string.Empty;
    }

    protected virtual bool GetHUDStatValueAsFloat(ItemInventoryData invData, out float value)
    {
        value = 0f;
        var data = GetDataFromInvData<MetadataAsHUDStatData>(invData);
        if (data != null)
        {
            object metaValue = invData.itemValue.GetMetadata(data.metaName);
            if (metaValue is float floatValue)
            {
                value = floatValue;
                return true;
            }
            else if (metaValue is int intValue)
            {
                value = intValue;
                return true;
            }
        }
        return false;
    }

    public virtual string GetHUDStatValueWithMax(ItemInventoryData invData, int currentAmmoCount)
    {
        var data = GetDataFromInvData<MetadataAsHUDStatData>(invData);
        if (data != null)
        {
            if (!string.IsNullOrEmpty(data.formatWithMax) && GetHUDStatValueAsFloat(invData, out float value))
            {
                if (data.isPerc)
                {
                    if (currentAmmoCount != 0)
                    {
                        return string.Format(data.formatWithMax, value * 100f / currentAmmoCount, value, currentAmmoCount);
                    }
                }
                else
                {
                    return string.Format(data.formatWithMax, value, currentAmmoCount);
                }
            }
        }

        return $"0/{currentAmmoCount}";
    }

    public virtual bool UpdateActiveItemAmmo(ItemInventoryData invData, ref int currentAmmoCount)
    {
        currentAmmoCount = 0;
        return true;
    }

    protected T GetDataFromInvData<T>(ItemInventoryData invData) where T : MetadataAsHUDStatData
    {
        return (invData.actionData[actionIndex] as IModuleContainerFor<T>)?.Instance;
    }

    protected ItemActionData GetActionDataFromInvData(ItemInventoryData invData)
    {
        return invData.actionData[actionIndex];
    }

    public virtual void GetIconOverride(ItemInventoryData invData, ref string originalIcon)
    {
        var data = GetDataFromInvData<MetadataAsHUDStatData>(invData);
        if (data != null && !string.IsNullOrEmpty(data.iconOverride))
        {
            originalIcon = data.iconOverride;
        }
    }

    public virtual void GetIconTintOverride(ItemInventoryData invData, ref Color32 originalTint)
    {
        var data = GetDataFromInvData<MetadataAsHUDStatData>(invData);
        if (data != null && data.iconTintOverride.HasValue)
        {
            originalTint = data.iconTintOverride.Value;
        }
    }

    public class MetadataAsHUDStatData
    {
        public string metaName = "";
        public string format = "{0:0}";
        public string formatWithMax = "{0:0}/{1:0}";
        public bool isPerc = false;
        public string iconOverride = "";
        public Color? iconTintOverride;
    }
}