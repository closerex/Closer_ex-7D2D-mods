using System;
using System.Collections.Generic;
using UnityEngine;

public class ConsoleCmdCalibrateWeapon : ConsoleCmdAbstract
{
    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        if (!_senderInfo.IsLocalGame || _params.Count < 2)
        {
            Log.Error("too few params: expecting 2 at least");
            return;
        }

        bool flag = Enum.TryParse<CalibrateType>(_params[0], out var calType);
        if (!flag)
        {
            Log.Error("Only following commands are valid: " + string.Join(",", Enum.GetNames(typeof(CalibrateType))));
            return;
        }

        flag = Enum.TryParse<TweakType>(_params[1], out var tweakType);
        if (!flag)
        {
            Log.Error("Only following tweak type are valid: " + String.Join(",", Enum.GetNames(typeof(TweakType))));
            return;
        }

        Transform targetTrans = null;
        var inv = GameManager.Instance.World.GetPrimaryPlayer().inventory;
        if (calType != CalibrateType.offset)
        {
            var weaponTrans = inv.GetHoldingItemTransform();
            if (weaponTrans == null)
            {
                Log.Error("player is not holding anything!");
                return;
            }

            targetTrans = weaponTrans.Find(_params[2]);
            if (targetTrans == null)
            {
                Log.Error("transform not found on weapon!");
                return;
            }
        }

        Vector3 param = Vector3.zero;
        if (tweakType != TweakType.log)
        {
            int parseIndex;
            if (calType != CalibrateType.offset)
            {
                parseIndex = 3;
                if (_params.Count < 4)
                {
                    Log.Error("relative or absolute value is required to calibrate!");
                    return;
                }
            }
            else
            {
                parseIndex = 2;
                if (_params.Count < 3)
                {
                    Log.Error("offset value is required to calibrate!");
                    return;
                }
            }

            if (_params.Count < parseIndex + 2)
                param = StringParsers.ParseVector3(_params[parseIndex]);
            else if (_params.Count == parseIndex + 2)
            {
                flag = float.TryParse(_params[parseIndex + 1], out float value);
                if (!flag)
                {
                    Log.Error("offset value is NAN!");
                    return;
                }

                switch (_params[parseIndex])
                {
                    case "x":
                        param.x = value;
                        break;
                    case "y":
                        param.y = value;
                        break;
                    case "z":
                        param.z = value;
                        break;
                    default:
                        Log.Error("must specify x/y/z axis!");
                        return;
                }
            }
            else
            {
                Log.Error("too many params!");
                return;
            }
        }

        switch (calType)
        {
            case CalibrateType.pos:
                targetTrans.localPosition = DoCalibrate(tweakType, targetTrans.localPosition, param);
                break;
            case CalibrateType.rot:
                targetTrans.localEulerAngles = DoCalibrate(tweakType, targetTrans.localEulerAngles, param);
                break;
            case CalibrateType.scale:
                targetTrans.localScale = DoCalibrate(tweakType, targetTrans.localScale, param);
                break;
            case CalibrateType.offset:
                //var zoomAction = Convert.ChangeType(inv.holdingItemData.actionData[1], typeof(ItemActionZoom).GetNestedType("ItemActionDataZoom", System.Reflection.BindingFlags.NonPublic));
                if (!(inv.holdingItemData.actionData[1] is ItemActionZoom.ItemActionDataZoom zoomAction))
                {
                    Log.Error("holding item can not aim!");
                    return;
                }
                zoomAction.ScopeCameraOffset = DoCalibrate(tweakType, zoomAction.ScopeCameraOffset, param);
                break;
        }
    }

    private Vector3 DoCalibrate(TweakType type, Vector3 origin, Vector3 param)
    {
        Vector3 res = origin;
        switch (type)
        {
            case TweakType.abs:
                res = param;
                break;
            case TweakType.rel:
                res = origin + param;
                break;
            case TweakType.log:
                Log.Out(res.ToString("F6"));
                break;
        }
        return res;
    }

    public override string[] getCommands()
    {
        return new string[] { "calibrate", "calib" };
    }

    public override string getDescription()
    {
        return "adjust weapon transform rotation, position, scale, scope offset in game and print current value for xml editing purpose.";
    }

    public override bool IsExecuteOnClient => true;

    public override int DefaultPermissionLevel => 1000;

    private enum CalibrateType
    {
        pos,
        rot,
        scale,
        offset
    }

    private enum TweakType
    {
        abs,
        rel,
        log
    }
}

