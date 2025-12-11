using System.Xml.Linq;
using UnityEngine;

public class IsTargetInAngle : TargetedCompareRequirementBase
{
    private bool useBoxRange = false;
    private Vector4 checkRange = Vector4.zero; //offset ver/hor, range ver/hor
    private float distance = 0f;
    private static bool debug = false;

    public static bool IsPointInRange(Vector3 directionToTarget, Vector3 forward, Vector3 right, Vector3 up, Vector2 rectSize)
    {
        Vector3 localPos = new Vector3(Vector3.Dot(directionToTarget, right), Vector3.Dot(directionToTarget, up), Vector3.Dot(directionToTarget, forward));
        if (debug)
            Log.Out($"IsPointInRange: directionToTarget {directionToTarget} forward {forward} right {right} up {up} localPos {localPos} rectSize {rectSize}");
        //Log.Out($"checking in range: local pos: {localPos} range {rectSize}");
        if (localPos.z <= 0f)
        {
            return false;
        }
        return Mathf.Abs(localPos.x) <= rectSize.x * 0.5f && Mathf.Abs(localPos.y) <= rectSize.y * 0.5f;
    }

    public static bool IsPointInPyramidAngle(Vector3 directionToTarget, Vector3 forward, Vector3 right, Vector3 up, Vector2 anglesHor, Vector2 anglesVer)
    {
        float totalAngle = Vector3.Angle(forward, directionToTarget);

        Vector3 projDir = Vector3.ProjectOnPlane(directionToTarget, up).normalized;
        float horizontalAngle = Vector3.SignedAngle(forward, projDir, up);
        if (debug)
            Log.Out($"IsPointInPyramidAngle: directionToTarget {directionToTarget} forward {forward} right {right} up {up} horizontalAngle {horizontalAngle} anglesHor {anglesHor} totalAngle {totalAngle}");
        if (horizontalAngle < anglesHor.x || horizontalAngle > anglesHor.y)
        {
            return false;
        }

        projDir = Vector3.ProjectOnPlane(directionToTarget, right).normalized;
        float verticalAngle = Vector3.Dot(forward, projDir) > 0 ? Vector3.SignedAngle(forward, projDir, right) : Vector3.SignedAngle(-forward, projDir, -right);
        //if (Mathf.Abs(verticalAngle) > 90f)
        //{
        //    verticalAngle = Mathf.Sign(verticalAngle) * (180f - Mathf.Abs(verticalAngle));
        //}
        if (debug)
            Log.Out($"IsPointInPyramidAngle: directionToTarget {directionToTarget} forward {forward} right {right} up {up} verticalAngle {verticalAngle} anglesVer {anglesVer} totalAngle {totalAngle}");
        return verticalAngle >= anglesVer.x && verticalAngle <= anglesVer.y;
    }

    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
        {
            return false;
        }

        if (_params.Self == null)
        {
            return false;
        }

        Vector3 position, forward, up, right;
        if (_params.Self is EntityPlayerLocal player)
        {
            position = player.playerCamera.transform.position;
            forward = player.playerCamera.transform.forward;
            up = player.playerCamera.transform.up;
            right = player.playerCamera.transform.right;
        }
        else
        {
            position = _params.Self.getHeadPosition() - Origin.position;
            forward = _params.Self.GetForwardVector();
            up = _params.Self.transform.up;
            right = _params.Self.transform.right;
        }
        Vector3 directionToTarget = target.getHeadPosition() - Origin.position - position;
        if (distance > 0f && directionToTarget.sqrMagnitude > distance * distance)
        {
            return invert;
        }
        bool isInAngle;
        if (useBoxRange)
        {
            Quaternion angleOffset = Quaternion.Euler(0f, checkRange.y, 0f) * Quaternion.Euler(checkRange.x, 0f, 0f);
            isInAngle = IsPointInRange(directionToTarget.normalized, angleOffset * forward, angleOffset * right, angleOffset * up, new Vector2(checkRange.w, checkRange.z));
            if (debug)
                Log.Out($"IsTargetInAngle: target {target.GetDebugName()} directionToTarget {directionToTarget.normalized} forward {forward} right {right} up {up} range {new Vector2(checkRange.w, checkRange.z)} isInAngle {isInAngle}");
        }
        else
        {
            isInAngle = IsPointInPyramidAngle(directionToTarget.normalized, forward, right, up, new Vector2(checkRange.y - checkRange.w * 0.5f, checkRange.y + checkRange.w * 0.5f), new Vector2(checkRange.x - checkRange.z * 0.5f, checkRange.x + checkRange.z * 0.5f));
            if (debug)
                Log.Out($"IsTargetInAngle: target {target.GetDebugName()} directionToTarget {directionToTarget.normalized} forward {forward} right {right} up {up} anglesHor {new Vector2(checkRange.y - checkRange.w * 0.5f, checkRange.y + checkRange.w * 0.5f)} anglesVer {new Vector2(checkRange.x - checkRange.z * 0.5f, checkRange.x + checkRange.z * 0.5f)} isInAngle {isInAngle}");
        }
        return isInAngle ^ invert;
    }

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        bool flag = base.ParseXAttribute(_attribute);
        if (!flag)
        {
            switch (_attribute.Name.LocalName)
            {
                case "use_box_range":
                    useBoxRange = bool.Parse(_attribute.Value);
                    flag = true;
                    break;
                case "range":
                    checkRange = StringParsers.ParseVector4(_attribute.Value);
                    flag = true;
                    break;
                case "distance":
                    distance = float.Parse(_attribute.Value);
                    flag = true;
                    break;
            }
        }
        targetType = TargetTypes.other;
        return flag;
    }
}
