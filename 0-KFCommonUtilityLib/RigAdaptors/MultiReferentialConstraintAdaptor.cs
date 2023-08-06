using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Animations.Rigging;
using UnityEngine;

public class MultiReferentialConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private int m_Driver;
    [SerializeField]
    private List<Transform> m_SourceObjects;

    public override void FindRigTargets()
    {
        var constraint = GetComponent<MultiReferentialConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.driver = m_Driver;
        constraint.data.sourceObjects = m_SourceObjects;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<MultiReferentialConstraint>();
        weight = constraint.weight;
        m_Driver = constraint.data.driver;
        m_SourceObjects = constraint.data.sourceObjects;
    }
}
