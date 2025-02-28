using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class DampedTransformAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_ConstrainedObject;
    [SerializeField]
    private Transform m_Source;
    [SerializeField]
    private float m_DampPosition;
    [SerializeField]
    private float m_DampRotation;
    [SerializeField]
    private bool m_MaintainAim;
    public override void FindRigTargets()
    {
        var constraint = GetComponent<DampedTransform>();
        weight = constraint.weight;
        m_ConstrainedObject = constraint.data.constrainedObject?.name;
        m_Source = constraint.data.sourceObject;
        m_DampPosition = constraint.data.dampPosition;
        m_DampRotation = constraint.data.dampRotation;
        m_MaintainAim = constraint.data.maintainAim;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<DampedTransform>();
        constraint.weight = weight;
        constraint.data.constrainedObject = targetRoot.FindInAllChildren(m_ConstrainedObject);
        constraint.data.sourceObject = m_Source;
        constraint.data.dampPosition = m_DampPosition;
        constraint.data.dampRotation = m_DampRotation;
        constraint.data.maintainAim = m_MaintainAim;
    }
}
