using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class MultiRotationConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_ConstrainedObject;
    [SerializeField]
    private WeightedTransformArray m_SourceObjects;
    [SerializeField]
    private Vector3 m_Offset;
    [SerializeField]
    private Vector3Bool m_ConstrainedAxes;
    [SerializeField]
    private bool m_MaintainOffset;

    public override void FindRigTargets()
    {
        var constraint = GetComponent<MultiRotationConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.constrainedObject = targetRoot.FindInAllChildren(m_ConstrainedObject);
        constraint.data.sourceObjects = m_SourceObjects;
        constraint.data.offset = m_Offset;
        constraint.data.constrainedXAxis = m_ConstrainedAxes.x;
        constraint.data.constrainedYAxis = m_ConstrainedAxes.y;
        constraint.data.constrainedZAxis = m_ConstrainedAxes.z;
        constraint.data.maintainOffset = m_MaintainOffset;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<MultiRotationConstraint>();
        weight = constraint.weight;
        m_ConstrainedObject = constraint.data.constrainedObject?.name;
        m_SourceObjects = constraint.data.sourceObjects;
        m_Offset = constraint.data.offset;
        m_ConstrainedAxes = new Vector3Bool(constraint.data.constrainedXAxis, constraint.data.constrainedYAxis, constraint.data.constrainedZAxis);
        m_MaintainOffset = constraint.data.maintainOffset;
    }
}
