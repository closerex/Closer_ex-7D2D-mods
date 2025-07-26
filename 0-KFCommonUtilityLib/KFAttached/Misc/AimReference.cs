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
    [SerializeField]
    private GameObject scopeBindingObject;
    public Transform alignmentTarget;
    public bool asReference;

    private void OnEnable()
    {
        scopeBase = GetComponentInParent<ScopeBase>();
        Transform refTrans = scopeBase ? scopeBase.transform : transform.parent;
        rotationOffset = Quaternion.Inverse(refTrans.rotation) * transform.rotation;
        positionOffset = refTrans.InverseTransformDirection(transform.position - refTrans.position);
        if (group)
        {
            group.UpdateEnableStates();
        }
    }

    private void OnDisable()
    {
        if (group)
        {
            group.UpdateEnableStates();
        }
    }

    public void UpdateEnableState(bool state)
    {
        if (scopeBindingObject)
        {
            scopeBindingObject.SetActive(state);
        }
    }
}