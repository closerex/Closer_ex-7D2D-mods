using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class BlendConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_ConstrainedObject;
    [SerializeField]
    private Transform m_SourceA;
    [SerializeField]
    private Transform m_SourceB;
    [SerializeField]
    private bool m_BlendPosition;
    [SerializeField]
    private bool m_BlendRotation;
    [SerializeField]
    private float m_PositionWeight;
    [SerializeField]
    private float m_RotationWeight;
    [SerializeField]
    private bool m_MaintainPositionOffsets;
    [SerializeField]
    private bool m_MaintainRotationOffsets;

    public override void ReadRigData()
    {
        var constraint = GetComponent<BlendConstraint>();
        weight = constraint.weight;
        m_ConstrainedObject = constraint.data.constrainedObject?.name;
        m_SourceA = constraint.data.sourceObjectA;
        m_SourceB = constraint.data.sourceObjectB;
        m_BlendPosition = constraint.data.blendPosition;
        m_BlendRotation = constraint.data.blendRotation;
        m_PositionWeight = constraint.data.positionWeight;
        m_RotationWeight = constraint.data.rotationWeight;
        m_MaintainPositionOffsets = constraint.data.maintainPositionOffsets;
        m_MaintainRotationOffsets = constraint.data.maintainRotationOffsets;
    }

    public override void FindRigTargets()
    {
        var constraint = GetComponent<BlendConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.constrainedObject = targetRoot.FindInAllChildren(m_ConstrainedObject);
        constraint.data.sourceObjectA = m_SourceA;
        constraint.data.sourceObjectB = m_SourceB;
        constraint.data.blendPosition = m_BlendPosition;
        constraint.data.blendRotation = m_BlendRotation;
        constraint.data.positionWeight = m_PositionWeight;
        constraint.data.rotationWeight = m_RotationWeight;
        constraint.data.maintainPositionOffsets = m_MaintainPositionOffsets;
        constraint.data.maintainRotationOffsets = m_MaintainRotationOffsets;
    }
}
