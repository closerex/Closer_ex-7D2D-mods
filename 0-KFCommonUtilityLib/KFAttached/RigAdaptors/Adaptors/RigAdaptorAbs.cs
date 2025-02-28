using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public abstract class RigAdaptorAbs : MonoBehaviour
{
    [NonSerialized]
    public Transform targetRoot;
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

    protected WeightedTransformArray WeightedTransformArrayFromAdaptor(Transform targetRoot, string[] transforms, float[] weights)
    {
        WeightedTransformArray array = new WeightedTransformArray();
        for (int i = 0; i < transforms.Length; i++)
        {
            array.Add(new WeightedTransform(targetRoot.FindInAllChildren(transforms[i]), weights[i]));
        }
        return array;
    }
}
