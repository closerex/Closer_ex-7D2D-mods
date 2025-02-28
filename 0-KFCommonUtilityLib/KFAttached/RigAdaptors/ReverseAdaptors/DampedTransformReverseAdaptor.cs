using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class DampedTransformReverseAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private Transform m_ConstrainedObject;
    [SerializeField]
    private string m_Source;
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
        m_ConstrainedObject = constraint.data.constrainedObject;
        m_Source = constraint.data.sourceObject?.name;
        m_DampPosition = constraint.data.dampPosition;
        m_DampRotation = constraint.data.dampRotation;
        m_MaintainAim = constraint.data.maintainAim;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<DampedTransform>();
        constraint.weight = weight;
        constraint.data.constrainedObject = m_ConstrainedObject;
        constraint.data.sourceObject = targetRoot.FindInAllChildren(m_Source);
        constraint.data.dampPosition = m_DampPosition;
        constraint.data.dampRotation = m_DampRotation;
        constraint.data.maintainAim = m_MaintainAim;
    }
}
