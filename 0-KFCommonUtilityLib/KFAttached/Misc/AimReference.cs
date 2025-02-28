using System;
using UnityEngine;

public class AimReference : MonoBehaviour
{
    [NonSerialized]
    public Vector3 positionOffset;
    [NonSerialized]
    public Quaternion rotationOffset;
    [NonSerialized]
    public AimReferenceGroup group;
    [NonSerialized]
    public int index = -1;
    [NonSerialized]
    public ScopeBase scopeBase;
    public bool asReference;

    private void Awake()
    {
        scopeBase = GetComponentInParent<ScopeBase>();
        Transform refTrans = scopeBase ? scopeBase.transform : transform;
        rotationOffset = Quaternion.Inverse(refTrans.rotation) * transform.rotation;
        Vector3 positionOffset = transform.position - refTrans.position;
        this.positionOffset = new Vector3(Vector3.Dot(positionOffset, refTrans.right), Vector3.Dot(positionOffset, refTrans.up), Vector3.Dot(positionOffset, refTrans.forward));
    }

    private void OnEnable()
    {
        if (!group)
        {
            return;
        }

        group.UpdateEnableStates();
    }

    private void OnDisable()
    {
        if (!group)
        {
            return;
        }
        group.UpdateEnableStates();
    }
}