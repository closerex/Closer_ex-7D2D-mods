using KFCommonUtilityLib.Scripts.Attributes;
using UnityEngine;

[TypeTarget(typeof(ItemActionZoom), typeof(VariableZoomData))]
public class ActionModuleVariableZoom
{
    public static float zoomScale = 7.5f;
    [MethodTargetPostfix(nameof(ItemActionZoom.ConsumeScrollWheel))]
    private void Postfix_ConsumeScrollWheel(ItemActionData _actionData, float _scrollWheelInput, PlayerActionsLocal _playerInput, VariableZoomData __customData)
    {
        if (!_actionData.invData.holdingEntity.AimingGun || _scrollWheelInput == 0f)
        {
            return;
        }

        ItemActionZoom.ItemActionDataZoom itemActionDataZoom = (ItemActionZoom.ItemActionDataZoom)_actionData;
        if (!itemActionDataZoom.bZoomInProgress)
        {
            __customData.curScale = Utils.FastClamp(__customData.curScale + _scrollWheelInput * zoomScale, __customData.minScale, __customData.maxScale);
            __customData.shouldUpdate = true;
        }
    }

    [MethodTargetPostfix(nameof(ItemActionZoom.OnModificationsChanged))]
    private void Postfix_OnModificationChanged(ItemActionZoom __instance, ItemActionData _data, VariableZoomData __customData)
    {
        string str = __instance.Properties.GetString("ZoomRatio");
        if (string.IsNullOrEmpty(str))
        {
            str = "1";
        }
        __customData.maxScale = StringParsers.ParseFloat(_data.invData.itemValue.GetPropertyOverride("ZoomRatio", str));

        str = __instance.Properties.GetString("ZoomRatioMin");
        if (string.IsNullOrEmpty(str))
        {
            str = __customData.maxScale.ToString();
        }
        __customData.minScale = StringParsers.ParseFloat(_data.invData.itemValue.GetPropertyOverride("ZoomRatioMin", str));
        __customData.curScale = Utils.FastClamp(__customData.curScale, __customData.minScale, __customData.maxScale);
        __customData.shouldUpdate = true;
    }

    public class VariableZoomData
    {
        public float maxScale = 1f;
        public float minScale = 1f;
        public float curScale = 1f;
        public bool shouldUpdate = true;
        public VariableZoomData(ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleVariableZoom _module)
        {

        }
    }
}
