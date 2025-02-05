﻿using System;
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

    public static IAnimatorWrapper GetWrapperForParam(this Animator self, AnimatorControllerParameter param, bool prefVanilla = false)
    {
        if (!self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            return AnimationGraphBuilder.DummyWrapper;
        switch (builder.GetWrapperRoleByParam(param))
        {
            case AnimationGraphBuilder.ParamInWrapper.Both:
                if (prefVanilla)
                {
                    return builder.VanillaWrapper;
                }
                else
                {
                    return builder.WeaponWrapper;
                }
            case AnimationGraphBuilder.ParamInWrapper.Vanilla:
                return builder.VanillaWrapper;
            case AnimationGraphBuilder.ParamInWrapper.Weapon:
                return builder.WeaponWrapper;
            default:
                return AnimationGraphBuilder.DummyWrapper;
        }
    }

    public static IAnimatorWrapper GetWrapperForParamHash(this Animator self, int nameHash, bool prefVanilla = false)
    {
        if (!self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            return AnimationGraphBuilder.DummyWrapper;
        switch (builder.GetWrapperRoleByParamHash(nameHash))
        {
            case AnimationGraphBuilder.ParamInWrapper.Both:
                if (prefVanilla)
                {
                    return builder.VanillaWrapper;
                }
                else
                {
                    return builder.WeaponWrapper;
                }
            case AnimationGraphBuilder.ParamInWrapper.Vanilla:
                return builder.VanillaWrapper;
            case AnimationGraphBuilder.ParamInWrapper.Weapon:
                return builder.WeaponWrapper;
            default:
                return AnimationGraphBuilder.DummyWrapper;
        }
    }

    public static IAnimatorWrapper GetWrapperForParamName(this Animator self, string name, bool prefVanilla = false)
    {
        if (!self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            return AnimationGraphBuilder.DummyWrapper;
        switch (builder.GetWrapperRoleByParamName(name))
        {
            case AnimationGraphBuilder.ParamInWrapper.Both:
                if (prefVanilla)
                {
                    return builder.VanillaWrapper;
                }
                else
                {
                    return builder.WeaponWrapper;
                }
            case AnimationGraphBuilder.ParamInWrapper.Vanilla:
                return builder.VanillaWrapper;
            case AnimationGraphBuilder.ParamInWrapper.Weapon:
                return builder.WeaponWrapper;
            default:
                return AnimationGraphBuilder.DummyWrapper;
        }
    }

    public static bool GetWrappedBool(this Animator self, int _propertyHash)
    {
        if (self)
        {
            var wrapper = self.GetWrapperForParamHash(_propertyHash);
            if (wrapper.IsValid)
            {
                return wrapper.GetBool(_propertyHash);
            }
            return self.GetBool(_propertyHash);
        }
        return false;
    }

    public static int GetWrappedInt(this Animator self, int _propertyHash)
    {
        if (self)
        {
            var wrapper = self.GetWrapperForParamHash(_propertyHash);
            if (wrapper.IsValid)
            {
                return wrapper.GetInteger(_propertyHash);
            }
            return self.GetInteger(_propertyHash);
        }
        return 0;
    }

    public static float GetWrappedFloat(this Animator self, int _propertyHash)
    {
        if (self)
        {
            var wrapper = self.GetWrapperForParamHash(_propertyHash);
            if (wrapper.IsValid)
            {
                return wrapper.GetFloat(_propertyHash);
            }
            return self.GetFloat(_propertyHash);
        }
        return float.NaN;
    }

    public static void SetWrappedTrigger(this Animator self, int _propertyHash)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            {
                var role = builder.GetWrapperRoleByParamHash(_propertyHash);
                switch(role)
                {
                    case AnimationGraphBuilder.ParamInWrapper.Both:
                        builder.VanillaWrapper.SetTrigger(_propertyHash);
                        builder.WeaponWrapper.SetTrigger(_propertyHash);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Vanilla:
                        builder.VanillaWrapper.SetTrigger(_propertyHash);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Weapon:
                        builder.WeaponWrapper.SetTrigger(_propertyHash);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                self.SetTrigger(_propertyHash);
            }
        }
    }

    public static void ResetWrappedTrigger(this Animator self, int _propertyHash)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            {
                var role = builder.GetWrapperRoleByParamHash(_propertyHash);
                switch (role)
                {
                    case AnimationGraphBuilder.ParamInWrapper.Both:
                        builder.VanillaWrapper.ResetTrigger(_propertyHash);
                        builder.WeaponWrapper.ResetTrigger(_propertyHash);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Vanilla:
                        builder.VanillaWrapper.ResetTrigger(_propertyHash);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Weapon:
                        builder.WeaponWrapper.ResetTrigger(_propertyHash);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                self.ResetTrigger(_propertyHash);
            }
        }
    }

    public static void SetWrappedBool(this Animator self, int _propertyHash, bool _value)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            {
                var role = builder.GetWrapperRoleByParamHash(_propertyHash);
                switch (role)
                {
                    case AnimationGraphBuilder.ParamInWrapper.Both:
                        builder.VanillaWrapper.SetBool(_propertyHash, _value);
                        builder.WeaponWrapper.SetBool(_propertyHash, _value);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Vanilla:
                        builder.VanillaWrapper.SetBool(_propertyHash, _value);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Weapon:
                        builder.WeaponWrapper.SetBool(_propertyHash, _value);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                self.SetBool(_propertyHash, _value);
            }
        }
    }

    public static void SetWrappedInt(this Animator self, int _propertyHash, int _value)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            {
                var role = builder.GetWrapperRoleByParamHash(_propertyHash);
                switch (role)
                {
                    case AnimationGraphBuilder.ParamInWrapper.Both:
                        builder.VanillaWrapper.SetInteger(_propertyHash, _value);
                        builder.WeaponWrapper.SetInteger(_propertyHash, _value);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Vanilla:
                        builder.VanillaWrapper.SetInteger(_propertyHash, _value);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Weapon:
                        builder.WeaponWrapper.SetInteger(_propertyHash, _value);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                self.SetInteger(_propertyHash, _value);
            }
        }
    }

    public static void SetWrappedFloat(this Animator self, int _propertyHash, float _value)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            {
                var role = builder.GetWrapperRoleByParamHash(_propertyHash);
                switch (role)
                {
                    case AnimationGraphBuilder.ParamInWrapper.Both:
                        builder.VanillaWrapper.SetFloat(_propertyHash, _value);
                        builder.WeaponWrapper.SetFloat(_propertyHash, _value);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Vanilla:
                        builder.VanillaWrapper.SetFloat(_propertyHash, _value);
                        break;
                    case AnimationGraphBuilder.ParamInWrapper.Weapon:
                        builder.WeaponWrapper.SetFloat(_propertyHash, _value);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                self.SetFloat(_propertyHash, _value);
            }
        }
    }

    public static AnimatorControllerParameter[] GetWrappedParameters(this  Animator self)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder) && builder.HasWeaponOverride)
            {
                return builder.Parameters;
            }
            return self.parameters;
        }
        return null;
    }

    public static IAnimatorWrapper GetItemAnimatorWrapper(this Animator self)
    {
        if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            return builder.WeaponWrapper;
        return new AnimatorWrapper(self);
    }

    public static bool IsVanillaInTransition(this Animator self, int layerIndex)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            {
                return builder.VanillaWrapper.IsInTransition(layerIndex);
            }
            return self.IsInTransition(layerIndex);
        }
        return false;
    }

    public static AnimatorStateInfo GetCurrentVanillaStateInfo(this Animator self, int layerIndex)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            {
                return builder.VanillaWrapper.GetCurrentAnimatorStateInfo(layerIndex);
            }
            return self.GetCurrentAnimatorStateInfo(layerIndex);
        }
        return default;
    }

    public static void SetVanillaLayerWeight(this Animator self, int layerIndex, float weight)
    {
        if (self)
        {
            if (self.TryGetComponent<AnimationGraphBuilder>(out var builder))
            {
                builder.VanillaWrapper.SetLayerWeight(layerIndex, weight);
            }
            else
            {
                self.SetLayerWeight(layerIndex, weight);
            }
        }
    }
}