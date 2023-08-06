using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class RigConverter : MonoBehaviour
{
    public Transform targetRoot;

    [ContextMenu("Convert Rig Constraints to Adaptors")]
    private void Convert()
    {
        foreach (var constraint in GetComponentsInChildren<IRigConstraint>())
        {
            var adaptorName = constraint.GetType().Name + "Adaptor,RigAdaptors";
            var adaptorType = Type.GetType(adaptorName);
            var adaptor = ((object)constraint as MonoBehaviour).transform.gameObject.AddComponent(adaptorType) as RigAdaptorAbs;
            adaptor.ReadRigData();
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        foreach (var adaptor in GetComponentsInChildren<RigAdaptorAbs>())
        {
            adaptor.targetRoot = targetRoot;
            adaptor.FindRigTargets();
        }
    }
}
