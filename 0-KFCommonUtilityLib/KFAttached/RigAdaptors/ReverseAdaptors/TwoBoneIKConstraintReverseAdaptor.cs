using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class TwoBoneIKConstraintReverseAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private Transform m_Root;
    [SerializeField]
    private Transform m_Mid;
    [SerializeField]
    private Transform m_Tip;
    [SerializeField]
    private string m_Target;
    [SerializeField]
    private string m_Hint;
    [SerializeField]
    private float m_TargetPositionWeight;
    [SerializeField]
    private float m_TargetRotationWeight;
    [SerializeField]
    private float m_HintWeight;
    [SerializeField]
    private bool m_MaintainTargetPositionOffset;
    [SerializeField]
    private bool m_MaintainTargetRotationOffset;
    public override void FindRigTargets()
    {
        var constraint = GetComponent<TwoBoneIKConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.root = m_Root;
        constraint.data.mid = m_Mid;
        constraint.data.tip = m_Tip;
        constraint.data.target = targetRoot.FindInAllChildren(m_Target);
        constraint.data.hint = targetRoot.FindInAllChildren(m_Hint);
        constraint.data.targetPositionWeight = m_TargetPositionWeight;
        constraint.data.targetRotationWeight = m_TargetRotationWeight;
        constraint.data.hintWeight = m_HintWeight;
        constraint.data.maintainTargetPositionOffset = m_MaintainTargetPositionOffset;
        constraint.data.maintainTargetRotationOffset = m_MaintainTargetRotationOffset;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<TwoBoneIKConstraint>();
        weight = constraint.weight;
        m_Root = constraint.data.root;
        m_Mid = constraint.data.mid;
        m_Tip = constraint.data.tip;
        m_Target = constraint.data.target?.name;
        m_Hint = constraint.data.hint?.name;
        m_TargetPositionWeight = constraint.data.targetPositionWeight;
        m_TargetRotationWeight = constraint.data.targetRotationWeight;
        m_HintWeight = constraint.data.hintWeight;
        m_MaintainTargetPositionOffset = constraint.data.maintainTargetPositionOffset;
        m_MaintainTargetRotationOffset = constraint.data.maintainTargetRotationOffset;
    }
}
