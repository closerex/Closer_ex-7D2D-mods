﻿using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class MultiAimConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_ConstrainedObject;
    [SerializeField]
    private WeightedTransformArray m_SourceObjects;
    [SerializeField]
    private Vector3 m_Offset;
    [SerializeField]
    private Vector2 m_limits;
    [SerializeField]
    private MultiAimConstraintData.Axis m_AimAxis;
    [SerializeField]
    private MultiAimConstraintData.Axis m_UpAxis;
    [SerializeField]
    private MultiAimConstraintData.WorldUpType m_WorldUpType;
    [SerializeField]
    private string m_WorldUpObject;
    [SerializeField]
    private MultiAimConstraintData.Axis m_WorldUpAxis;
    [SerializeField]
    private bool m_MaintainOffset;
    [SerializeField]
    private Vector3Bool m_ConstrainedAxes;

    public override void FindRigTargets()
    {
        var constraint = GetComponent<MultiAimConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.constrainedObject = targetRoot.FindInAllChilds(m_ConstrainedObject);
        constraint.data.sourceObjects = m_SourceObjects;
        constraint.data.offset = m_Offset;
        constraint.data.limits = m_limits;
        constraint.data.aimAxis = m_AimAxis;
        constraint.data.upAxis = m_UpAxis;
        constraint.data.worldUpType = m_WorldUpType;
        if (!string.IsNullOrEmpty(m_WorldUpObject))
            constraint.data.worldUpObject = targetRoot.FindInAllChilds(m_WorldUpObject);
        constraint.data.worldUpAxis = m_WorldUpAxis;
        constraint.data.maintainOffset = m_MaintainOffset;
        constraint.data.constrainedXAxis = m_ConstrainedAxes.x;
        constraint.data.constrainedYAxis = m_ConstrainedAxes.y;
        constraint.data.constrainedZAxis = m_ConstrainedAxes.z;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<MultiAimConstraint>();
        weight = constraint.weight;
        m_ConstrainedObject = constraint.data.constrainedObject?.name;
        m_SourceObjects = constraint.data.sourceObjects;
        m_Offset = constraint.data.offset;
        m_limits = constraint.data.limits;
        m_AimAxis = constraint.data.aimAxis;
        m_UpAxis = constraint.data.upAxis;
        m_WorldUpType = constraint.data.worldUpType;
        if ((m_WorldUpType == MultiAimConstraintData.WorldUpType.ObjectUp || m_WorldUpType == MultiAimConstraintData.WorldUpType.ObjectRotationUp) && constraint.data.worldUpObject)
            m_WorldUpObject = constraint.data.worldUpObject.name;
        m_WorldUpAxis = constraint.data.worldUpAxis;
        m_MaintainOffset = constraint.data.maintainOffset;
        m_ConstrainedAxes = new Vector3Bool(constraint.data.constrainedXAxis, constraint.data.constrainedYAxis, constraint.data.constrainedZAxis);
    }
}
