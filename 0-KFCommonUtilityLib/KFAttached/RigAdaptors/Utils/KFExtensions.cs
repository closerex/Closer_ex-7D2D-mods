using System;
using UnityEngine;

public static class KFExtensions
{
    public static Transform FindInAllChilds(this Transform target, string name, bool onlyActive = false)
    {
        if (name == null)
        {
            return null;
        }

        if (!onlyActive || (onlyActive && (bool)target.gameObject && target.gameObject.activeSelf))
        {
            if (target.name == name)
            {
                return target;
            }

            for (int i = 0; i < target.childCount; i++)
            {
                Transform transform = target.GetChild(i).FindInAllChilds(name, onlyActive);
                if (transform != null)
                {
                    return transform;
                }
            }

            return null;
        }

        return null;
    }
    public static T AddMissingComponent<T>(this Transform transform) where T : Component
    {
        if (!transform.TryGetComponent<T>(out var val))
        {
            val = transform.gameObject.AddComponent<T>();
        }

        return val;
    }
    public static Component AddMissingComponent(this Transform transform, Type type)
    {
        if (!transform.TryGetComponent(type, out var val))
        {
            val = transform.gameObject.AddComponent(type);
        }

        return val;
    }
    public static void RotateAroundPivot(this Transform self, Transform pivot, Vector3 angles)
    {
        Vector3 dir = self.InverseTransformVector(self.position - pivot.position); // get point direction relative to pivot
        Quaternion rot = Quaternion.Euler(angles);
        dir = rot * dir; // rotate it
        self.localPosition = dir + self.InverseTransformPoint(pivot.position); // calculate rotated point
        self.localRotation = rot;
    }

    public static Vector3 Random(Vector3 min, Vector3 max)
    {
        return new Vector3(UnityEngine.Random.Range(min.x, max.x), UnityEngine.Random.Range(min.y, max.y), UnityEngine.Random.Range(min.z, max.z));
    }

    public static Vector3 Clamp(Vector3 val, Vector3 min, Vector3 max)
    {
        return new Vector3(Mathf.Clamp(val.x, min.x, max.x), Mathf.Clamp(val.y, min.y, max.y), Mathf.Clamp(val.z, min.z, max.z));
    }

    public static float AngleToInferior(float angle)
    {
        angle %= 360;
        angle = angle > 180 ? angle - 360 : angle;
        return angle;
    }

    public static Vector3 AngleToInferior(Vector3 angle)
    {
        return new Vector3(AngleToInferior(angle.x), AngleToInferior(angle.y), AngleToInferior(angle.z));
    }
}