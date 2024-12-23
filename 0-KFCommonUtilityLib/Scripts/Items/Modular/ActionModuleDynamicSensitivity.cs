using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using UnityEngine;

[TypeTarget(typeof(ItemActionZoom), typeof(DynamicSensitivityData))]
public class ActionModuleDynamicSensitivity
{
    [MethodTargetPostfix(nameof(ItemAction.AimingSet))]
    private void Postfix_AimingSet(ItemActionData _actionData, bool _isAiming, bool _wasAiming, DynamicSensitivityData __customData)
    {
        float originalSensitivity = GamePrefs.GetFloat(EnumGamePrefs.OptionsZoomSensitivity);
        if (_isAiming)
        {
            PlayerMoveController.Instance.mouseZoomSensitivity = originalSensitivity / Mathf.Sqrt(__customData.ZoomRatio);
        }
        else
        {
            PlayerMoveController.Instance.mouseZoomSensitivity = originalSensitivity;
        }
    }

    [MethodTargetPostfix(nameof(ItemAction.OnModificationsChanged))]
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
    }

    [MethodTargetPostfix(nameof(ItemAction.OnHoldingUpdate))]
    private void Postfix_OnHoldingUpdate(ItemActionData _actionData, DynamicSensitivityData __customData)
    {
        if (((ItemActionZoom.ItemActionDataZoom)_actionData).aimingValue)
        {
            float originalSensitivity = GamePrefs.GetFloat(EnumGamePrefs.OptionsZoomSensitivity);
            if (__customData.activated)
            {
                PlayerMoveController.Instance.mouseZoomSensitivity = originalSensitivity / Mathf.Sqrt(__customData.ZoomRatio);
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
        public bool activated = false;

        public float ZoomRatio { get => variableZoomData?.curScale ?? zoomRatio; set => zoomRatio = value; }

        public DynamicSensitivityData(ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleDynamicSensitivity _module)
        {

        }
    }
}