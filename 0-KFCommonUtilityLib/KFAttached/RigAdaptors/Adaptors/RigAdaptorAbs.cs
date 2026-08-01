using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public abstract class RigAdaptorAbs : MonoBehaviour
{
    [NonSerialized]
    public Transform targetRoot;
    [NonSerialized]
    public Transform weaponRoot;
    [SerializeField]
    protected float weight = 1f;
    public abstract void ReadRigData();
    public abstract void FindRigTargets();

    protected void WeightedTransformArrayToAdaptor(WeightedTransformArray array, out string[] transforms, out float[] weights)
    {
        transforms = new string[array.Count];
        weights = new float[array.Count];
        for (int i = 0; i < array.Count; i++)
        {
            transforms[i] = array[i].transform?.name;
            weights[i] = array[i].weight;
        }
    }

    protected WeightedTransformArray WeightedTransformArrayFromAdaptor(string[] transforms, float[] weights)
    {
        WeightedTransformArray array = new WeightedTransformArray();
        for (int i = 0; i < transforms.Length; i++)
        {
            array.Add(new WeightedTransform(targetRoot.FindInAllChildren(transforms[i]), weights[i]));
        }
        return array;
    }

    protected Transform ReplaceWeaponTarget(ref Transform original)
    {
#if UNITY_EDITOR
        if (weaponRoot == null || original == null || original.IsChildOf(weaponRoot))
        {
            return original;
        }

        Transform replaced = null;
        if (original.name.EndsWith("Target", StringComparison.InvariantCulture))
        {
            var parent = original.parent;
            if (ReplaceWeaponTarget(ref parent) != null)
            {
                replaced = parent.Find(original.name);
                if (replaced != null)
                {
                    Log.Out($"Replacing {original.name} with existing target on {transform.name}, adaptor {GetType().Name}");
                }
                else
                {
                    Log.Out($"Replacing {original.name} with new target on {transform.name}, adaptor {GetType().Name}");
                    replaced = new GameObject(original.name).transform;
                    replaced.parent = parent;
                    replaced.localPosition = original.localPosition;
                    replaced.localRotation = original.localRotation;
                    replaced.localScale = original.localScale;
                }
            }
        }
        else
        {
            replaced = weaponRoot.FindInAllChildren(original.name);
            if (replaced != null)
            {
                Log.Out($"Replacing {original.name} on {transform.name}, adaptor {GetType().Name}");
            }
        }

        if (replaced != null)
        {
            original = replaced;
        }
        else
        {
            Log.Warning($"Transform {original.name} not found on {transform.name}, adaptor {GetType().Name}");
        }
#endif
        return original;
    }

    protected WeightedTransformArray ReplaceWeaponTarget(ref WeightedTransformArray array)
    {
#if UNITY_EDITOR
        if (weaponRoot == null)
        {
            return array;
        }
        WeightedTransformArray temp = new();
        for (int i = 0; i < array.Count; i++)
        {
            Transform curTarget = array[i].transform;
            temp.Add(new WeightedTransform(ReplaceWeaponTarget(ref curTarget), array[i].weight));
        }
        array = temp;
#endif
        return array;
    }

    protected List<Transform> ReplaceWeaponTarget(List<Transform> targets)
    {
#if UNITY_EDITOR
        if (weaponRoot == null || targets == null || targets.Count == 0)
        {
            return targets;
        }
        for (int i = 0; i < targets.Count; i++)
        {
            Transform curTarget = targets[i];
            targets[i] = ReplaceWeaponTarget(ref curTarget);
        }
#endif
        return targets;
    }
}
