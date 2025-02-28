using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class OverrideTransformAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_ConstrainedObject;
    [SerializeField]
    private Transform m_OverrideSource;
    [SerializeField]
    private Vector3 m_OverridePosition;
    [SerializeField]
    private Vector3 m_OverrideRotation;
    [SerializeField]
    private float m_PositionWeight;
    [SerializeField]
    private float m_RotationWeight;
    [SerializeField]
    private OverrideTransformData.Space m_Space;

    public override void FindRigTargets()
    {
        var constraint = GetComponent<OverrideTransform>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.constrainedObject = targetRoot.FindInAllChildren(m_ConstrainedObject);
        constraint.data.sourceObject = m_OverrideSource;
        constraint.data.position = m_OverridePosition;
        constraint.data.rotation = m_OverrideRotation;
        constraint.data.positionWeight = m_PositionWeight;
        constraint.data.rotationWeight = m_RotationWeight;
        constraint.data.space = m_Space;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<OverrideTransform>();
        weight = constraint.weight;
        m_ConstrainedObject = constraint.data.constrainedObject?.name;
        m_OverrideSource = constraint.data.sourceObject;
        m_OverridePosition = constraint.data.position;
        m_OverrideRotation = constraint.data.rotation;
        m_PositionWeight = constraint.data.positionWeight;
        m_RotationWeight = constraint.data.rotationWeight;
        m_Space = constraint.data.space;
    }
}
