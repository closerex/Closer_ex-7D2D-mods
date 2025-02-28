using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class TwoBoneIKConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_Root;
    [SerializeField]
    private string m_Mid;
    [SerializeField]
    private string m_Tip;
    [SerializeField]
    private Transform m_Target;
    [SerializeField]
    private Transform m_Hint;
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
        constraint.data.root = targetRoot.FindInAllChildren(m_Root);
        constraint.data.mid = targetRoot.FindInAllChildren(m_Mid);
        constraint.data.tip = targetRoot.FindInAllChildren(m_Tip);
        constraint.data.target = m_Target;
        constraint.data.hint = m_Hint;
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
        m_Root = constraint.data.root?.name;
        m_Mid = constraint.data.mid?.name;
        m_Tip = constraint.data.tip?.name;
        m_Target = constraint.data.target;
        m_Hint = constraint.data.hint;
        m_TargetPositionWeight = constraint.data.targetPositionWeight;
        m_TargetRotationWeight = constraint.data.targetRotationWeight;
        m_HintWeight = constraint.data.hintWeight;
        m_MaintainTargetPositionOffset = constraint.data.maintainTargetPositionOffset;
        m_MaintainTargetRotationOffset = constraint.data.maintainTargetRotationOffset;
    }
}
