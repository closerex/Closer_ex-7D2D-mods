using System;
using UnityEngine;

public abstract class RigAdaptorAbs : MonoBehaviour
{
    [NonSerialized]
    public Transform targetRoot;
    [SerializeField]
    protected float weight = 1f;
    public abstract void ReadRigData();
    public abstract void FindRigTargets();
}
