using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using UnityEngine;

[TypeTarget(typeof(ItemActionZoom)), TypeDataTarget(typeof(DynamicSensitivityData))]
public class ActionModuleDynamicSensitivity
{
    [HarmonyPatch(nameof(ItemAction.AimingSet)), MethodTargetPostfix]
    private void Postfix_AimingSet(ItemActionData _actionData, bool _isAiming, bool _wasAiming, DynamicSensitivityData __customData)
    {
        float originalSensitivity = GamePrefs.GetFloat(EnumGamePrefs.OptionsZoomSensitivity);
        if (_isAiming)
        {
            PlayerMoveController.Instance.mouseZoomSensitivity = originalSensitivity / __customData.ZoomRatio;
        }
        else
        {
            PlayerMoveController.Instance.mouseZoomSensitivity = originalSensitivity;
        }
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationsChanged(ItemActionZoom __instance, ItemActionData _data, DynamicSensitivityData __customData)
    {
        if (_data is IModuleContainerFor<ActionModuleVariableZoom.VariableZoomData> variableZoomData)
        {
            __customData.variableZoomData = variableZoomData.Instance;
        }
        else
        {
            string str = __instance.Properties.GetString("ZoomRatio");
            if (string.IsNullOrEmpty(str))
            {
                str = "1";
            }
            __customData.ZoomRatio = StringParsers.ParseFloat(_data.invData.itemValue.GetPropertyOverride("ZoomRatio", str));
        }

        StringParsers.TryParseRange(_data.invData.itemValue.GetPropertyOverride("DynamicSensitivityRange", "0,0"), out __customData.dsRangeOverride);
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    private void Postfix_OnHoldingUpdate(ItemActionData _actionData, DynamicSensitivityData __customData)
    {
        if (((ItemActionZoom.ItemActionDataZoom)_actionData).aimingValue)
        {
            float originalSensitivity = GamePrefs.GetFloat(EnumGamePrefs.OptionsZoomSensitivity);
            if (__customData.activated)
            {
                PlayerMoveController.Instance.mouseZoomSensitivity = originalSensitivity / __customData.ZoomRatio;
            }
            else
            {
                PlayerMoveController.Instance.mouseZoomSensitivity = originalSensitivity;
            }
        }
    }

    public class DynamicSensitivityData
    {
        public ActionModuleVariableZoom.VariableZoomData variableZoomData = null;
        private float zoomRatio = 1.0f;
        public FloatRange dsRangeOverride;
        public bool activated = false;

        public float ZoomRatio
        {
            get
            {
                if (variableZoomData != null)
                {
                    if (dsRangeOverride.min > 0 && dsRangeOverride.max >= dsRangeOverride.min)
                    {
                        return Mathf.Lerp(dsRangeOverride.min, dsRangeOverride.max, Mathf.InverseLerp(variableZoomData.minScale, variableZoomData.maxScale, variableZoomData.curScale));
                    }
                    if (!variableZoomData.forceFov)
                    {
                        return variableZoomData.curScale;
                    }
                    return 1f;
                }
                return zoomRatio;
            }
            set => zoomRatio = value;
        }
    }
}