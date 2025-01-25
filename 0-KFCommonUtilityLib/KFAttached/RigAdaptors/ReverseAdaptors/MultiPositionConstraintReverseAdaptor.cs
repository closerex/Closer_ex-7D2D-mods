using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class MultiPositionConstraintReverseAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private Transform m_ConstrainedObject;
    [SerializeField]
    private string[] m_SourceObjectNames;
    [SerializeField]
    private float[] m_SourceObjectWeights;
    [SerializeField]
    private Vector3 m_Offset;
    [SerializeField]
    private Vector3Bool m_ConstrainedAxes;
    [SerializeField]
    private bool m_MaintainOffset;

    public override void FindRigTargets()
    {
        var constraint = GetComponent<MultiPositionConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.constrainedObject = m_ConstrainedObject;
        constraint.data.sourceObjects = WeightedTransformArrayFromAdaptor(targetRoot, m_SourceObjectNames, m_SourceObjectWeights);
        constraint.data.offset = m_Offset;
        constraint.data.constrainedXAxis = m_ConstrainedAxes.x;
        constraint.data.constrainedYAxis = m_ConstrainedAxes.y;
        constraint.data.constrainedZAxis = m_ConstrainedAxes.z;
        constraint.data.maintainOffset = m_MaintainOffset;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<MultiPositionConstraint>();
        weight = constraint.weight;
        m_ConstrainedObject = constraint.data.constrainedObject;
        WeightedTransformArrayToAdaptor(constraint.data.sourceObjects, out m_SourceObjectNames, out m_SourceObjectWeights);
        m_Offset = constraint.data.offset;
        m_ConstrainedAxes = new Vector3Bool(constraint.data.constrainedXAxis, constraint.data.constrainedYAxis, constraint.data.constrainedZAxis);
        m_MaintainOffset = constraint.data.maintainOffset;
    }
}
