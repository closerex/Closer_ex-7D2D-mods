using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class MultiReferentialConstraintReverseAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private int m_Driver;
    [SerializeField]
    private List<string> m_SourceObjects;

    public override void FindRigTargets()
    {
        var constraint = GetComponent<MultiReferentialConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.driver = m_Driver;
        constraint.data.sourceObjects = new List<Transform>();
        foreach(var sourceObject in m_SourceObjects)
        {
            constraint.data.sourceObjects.Add(targetRoot.FindInAllChildren(sourceObject));
        }
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<MultiReferentialConstraint>();
        weight = constraint.weight;
        m_Driver = constraint.data.driver;
        m_SourceObjects = new List<string>();
        foreach (var sourceObject in constraint.data.sourceObjects)
        {
            m_SourceObjects.Add(sourceObject?.name);
        }
    }
}
