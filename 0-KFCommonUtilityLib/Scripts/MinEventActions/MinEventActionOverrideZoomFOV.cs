using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;

public class MinEventActionOverrideZoomFOV : MinEventActionBase
{
    private int fov = 0;
    //private static Type zoomDataType = typeof(ItemActionZoom).GetNestedType("ItemActionDataZoom", System.Reflection.BindingFlags.NonPublic);
    //private static FieldInfo fldZoomInProgress = AccessTools.Field(zoomDataType, "bZoomInProgress");
    //private static FieldInfo fldTimeZoomStarted = AccessTools.Field(zoomDataType, "timeZoomStarted");
    //private static FieldInfo fldCurrentZoom = AccessTools.Field(zoomDataType, "CurrentZoom");
    //private static FieldInfo fldMaxZoomOut = AccessTools.Field(zoomDataType, "MaxZoomOut");
    //private static FieldInfo fldMaxZoomIn = AccessTools.Field(zoomDataType, "MaxZoomIn");
    //private static FieldInfo fldEndLerpFov = AccessTools.Field(typeof(EntityPlayerLocal), "lerpCameraEndFOV");
    //private static MethodInfo mtdUpdateCameraPosition = AccessTools.Method(typeof(EntityAlive), "updateCameraPosition");

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        if (!base.CanExecute(_eventType, _params))
            return false;

        return _params.Self is EntityPlayerLocal player && player?.inventory != null && player.inventory.holdingItemData?.actionData[1] is ItemActionZoom.ItemActionDataZoom;
    }

    public override void Execute(MinEventParams _params)
    {
        base.Execute(_params);
        var target = (EntityPlayerLocal)_params.Self;
        var zoomActionData = (ItemActionZoom.ItemActionDataZoom)target.inventory.holdingItemData.actionData[1];
        int targetFov = fov;
        int targetMax = fov;
        int targetMin = fov;
        //restore min max fov
        if (fov <= 0)
        {
            float fovSetting = (float)GamePrefs.GetInt(EnumGamePrefs.OptionsGfxFOV);
            ItemAction action = target.inventory.holdingItem.Actions[1];
            if (action.Properties != null && action.Properties.Values.ContainsKey("Zoom_max_out"))
            {
                targetMax = StringParsers.ParseSInt32(target.inventory.holdingItemData.itemValue.GetPropertyOverride("Zoom_max_out", action.Properties.Values["Zoom_max_out"]), 0, -1, NumberStyles.Integer);
            }
            else
            {
                targetMax = StringParsers.ParseSInt32(target.inventory.holdingItemData.itemValue.GetPropertyOverride("Zoom_max_out", fovSetting.ToString()), 0, -1, NumberStyles.Integer);
            }
            if (action.Properties != null && action.Properties.Values.ContainsKey("Zoom_max_in"))
            {
                targetMin = StringParsers.ParseSInt32(target.inventory.holdingItemData.itemValue.GetPropertyOverride("Zoom_max_in", action.Properties.Values["Zoom_max_in"]), 0, -1, NumberStyles.Integer);
            }
            else
            {
                targetMin = StringParsers.ParseSInt32(target.inventory.holdingItemData.itemValue.GetPropertyOverride("Zoom_max_in", fovSetting.ToString()), 0, -1, NumberStyles.Integer);
            }
            targetFov = targetMax;
        }

        zoomActionData.MaxZoomIn = targetMin;
        zoomActionData.MaxZoomOut = targetMax;
        zoomActionData.CurrentZoom = targetFov;
        //Log.Out($"setting zoom override max {targetMax} min {targetMin} cur {targetFov}");
        //if is aiming, lerp towards the final result
        if (target.AimingGun)
        {
            //if lerp not in progress, start lerp
            if (!target.bLerpCameraFlag)
            {
                zoomActionData.bZoomInProgress = true;
                zoomActionData.timeZoomStarted = Time.time;
                target.UpdateCameraFOV(true);
            }
            //if already in progress, set end value
            else
            {
                target.lerpCameraEndFOV = targetFov;
            }

            //Log.Out($"begin lerp camera");
        }
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if(base.ParseXmlAttribute(_attribute))
            return true;

        switch(_attribute.Name.LocalName)
        {
            case "value":
                fov = StringParsers.ParseSInt32(_attribute.Value);
                return true;
        }
        return false;
    }
}
