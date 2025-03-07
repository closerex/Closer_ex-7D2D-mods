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
    public bool asReference;

    private void Awake()
    {
        scopeBase = GetComponentInParent<ScopeBase>();
        Transform refTrans = scopeBase ? scopeBase.transform : transform.parent;
        rotationOffset = Quaternion.Inverse(refTrans.rotation) * transform.rotation;
        positionOffset = refTrans.InverseTransformDirection(transform.position - refTrans.position);
        //positionOffset = Matrix4x4.TRS(refTrans.position, refTrans.rotation, Vector3.one).inverse.MultiplyPoint3x4(transform.position);
        //this.positionOffset = new Vector3(Vector3.Dot(positionOffset, refTrans.right), Vector3.Dot(positionOffset, refTrans.up), Vector3.Dot(positionOffset, refTrans.forward));
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

    public void UpdateEnableState(bool state)
    {
        if (scopeBindingObject)
        {
            scopeBindingObject.SetActive(state);
        }
    }
}