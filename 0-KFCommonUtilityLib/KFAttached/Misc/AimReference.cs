using KFCommonUtilityLib;
using System;
using UnityEngine;

public class AimReference : MonoBehaviour
#if UNITY_EDITOR
    , ISerializationCallbackReceiver
#endif
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
    public Transform laserOriginOverride;
    public Transform akimboLaserOriginOverride;

    [Range(1f, 85f)]
    public float designedAimFov = 45;
    public float designedAimDistance = -1;
    [Range(0f, 1f)]
    public float designedFlattenFactor = 0;
    public bool applyAimFovCorrection = true;

#if UNITY_EDITOR
    public Transform aimDistanceTargetEditor;
    public void OnBeforeSerialize()
    {
        if (aimDistanceTargetEditor)
        {
            designedAimDistance = Vector3.Distance(aimDistanceTargetEditor.position, transform.position);
            aimDistanceTargetEditor = null;
        }
    }

    public void OnAfterDeserialize()
    {

    }
#endif

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