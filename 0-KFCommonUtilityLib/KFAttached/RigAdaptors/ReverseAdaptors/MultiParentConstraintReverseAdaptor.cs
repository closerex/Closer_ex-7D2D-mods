using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class MultiParentConstraintReverseAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private Transform m_ConstrainedObject;
    [SerializeField]
    private string[] m_SourceObjectNames;
    [SerializeField]
    private float[] m_SourceObjectWeights;
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
        constraint.data.constrainedObject = m_ConstrainedObject;
        constraint.data.sourceObjects = WeightedTransformArrayFromAdaptor(targetRoot, m_SourceObjectNames, m_SourceObjectWeights);
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
        m_ConstrainedObject = constraint.data.constrainedObject;
        WeightedTransformArrayToAdaptor(constraint.data.sourceObjects, out m_SourceObjectNames, out m_SourceObjectWeights);
        m_ConstrainedPositionAxes = new Vector3Bool(constraint.data.constrainedPositionXAxis, constraint.data.constrainedPositionYAxis, constraint.data.constrainedPositionZAxis);
        m_ConstrainedRotationAxes = new Vector3Bool(constraint.data.constrainedRotationXAxis, constraint.data.constrainedRotationYAxis, constraint.data.constrainedRotationZAxis);
        m_MaintainPositionOffset = constraint.data.maintainPositionOffset;
        m_MaintainRotationOffset = constraint.data.maintainRotationOffset;
    }
}
