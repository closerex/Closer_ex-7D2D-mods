using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class MultiParentConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_ConstrainedObject;
    [SerializeField]
    private WeightedTransformArray m_SourceObjects;
    [SerializeField]
    private Vector3Bool m_ConstrainedPositionAxes;
    [SerializeField]
    private Vector3Bool m_ConstrainedRotationAxes;
    [SerializeField]
    private bool m_MaintainPositionOffset;
    [SerializeField]
    private bool m_MaintainRotationOffset;
    public override void FindRigTargets()
    {
        var constraint = GetComponent<MultiParentConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.constrainedObject = targetRoot.FindInAllChildren(m_ConstrainedObject);
        constraint.data.sourceObjects = m_SourceObjects;
        constraint.data.constrainedPositionXAxis = m_ConstrainedPositionAxes.x;
        constraint.data.constrainedPositionYAxis = m_ConstrainedPositionAxes.y;
        constraint.data.constrainedPositionZAxis = m_ConstrainedPositionAxes.z;
        constraint.data.constrainedRotationXAxis = m_ConstrainedRotationAxes.x;
        constraint.data.constrainedRotationYAxis = m_ConstrainedRotationAxes.y;
        constraint.data.constrainedRotationZAxis = m_ConstrainedRotationAxes.z;
        constraint.data.maintainPositionOffset = m_MaintainPositionOffset;
        constraint.data.maintainRotationOffset = m_MaintainRotationOffset;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<MultiParentConstraint>();
        weight = constraint.weight;
        m_ConstrainedObject = constraint.data.constrainedObject?.name;
        m_SourceObjects = constraint.data.sourceObjects;
        m_ConstrainedPositionAxes = new Vector3Bool(constraint.data.constrainedPositionXAxis, constraint.data.constrainedPositionYAxis, constraint.data.constrainedPositionZAxis);
        m_ConstrainedRotationAxes = new Vector3Bool(constraint.data.constrainedRotationXAxis, constraint.data.constrainedRotationYAxis, constraint.data.constrainedRotationZAxis);
        m_MaintainPositionOffset = constraint.data.maintainPositionOffset;
        m_MaintainRotationOffset = constraint.data.maintainRotationOffset;
    }
}
